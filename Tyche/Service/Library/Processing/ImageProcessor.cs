using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyche.Service.Catalog;
using Tyche.Service.Configuration;
using Tyche.Service.Requests;

namespace Tyche.Service.Processing;

public sealed class ImageProcessor : IImageProcessor, IDisposable
{
  private readonly ImageApiOptions options;
  private readonly IMagickArgumentBuilder argumentBuilder;
  private readonly IProcessRunner runner;
  private readonly ILogger<ImageProcessor> logger;
  private readonly SemaphoreSlim concurrency;

  public ImageProcessor(
    IOptions<ImageApiOptions> options,
    IMagickArgumentBuilder argumentBuilder,
    IProcessRunner runner,
    ILogger<ImageProcessor> logger)
  {
    this.options = options.Value;
    this.argumentBuilder = argumentBuilder;
    this.runner = runner;
    this.logger = logger;
    concurrency = new SemaphoreSlim(this.options.MaxConcurrentProcesses);
  }

  public async Task ProcessAsync(
    CatalogImage source,
    ImageRequest request,
    string outputPath,
    CancellationToken cancellationToken)
  {
    var arguments = argumentBuilder.Build(source, request, outputPath);
    var timeout = TimeSpan.FromSeconds(options.ProcessTimeoutSeconds);

    await concurrency.WaitAsync(cancellationToken);
    try
    {
      var result = await runner.RunAsync(options.MagickPath, arguments, timeout, cancellationToken);
      if (result.ExitCode != 0)
      {
        logger.LogError(
          "ImageMagick failed for {Source} with exit code {ExitCode}: {Error}",
          source.Path,
          result.ExitCode,
          result.StandardError);
        throw new ImageProcessingException($"ImageMagick exited with code {result.ExitCode}.");
      }
    }
    finally
    {
      concurrency.Release();
    }
  }

  public void Dispose() => concurrency.Dispose();
}
