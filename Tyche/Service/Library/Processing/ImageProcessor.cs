using Microsoft.Extensions.Options;
using Tyche.Service.Catalog;
using Tyche.Service.Configuration;
using Tyche.Service.Requests;

namespace Tyche.Service.Processing;

public sealed class ImageProcessor : IImageProcessor, IDisposable
{
  private readonly ImageApiOptions _options;
  private readonly IMagickArgumentBuilder _argumentBuilder;
  private readonly IProcessRunner _runner;
  private readonly ILogger<ImageProcessor> _logger;
  private readonly SemaphoreSlim _concurrency;

  public ImageProcessor(
    IOptions<ImageApiOptions> options,
    IMagickArgumentBuilder argumentBuilder,
    IProcessRunner runner,
    ILogger<ImageProcessor> logger)
  {
    _options = options.Value;
    _argumentBuilder = argumentBuilder;
    _runner = runner;
    _logger = logger;
    _concurrency = new SemaphoreSlim(_options.MaxConcurrentProcesses);
  }

  public async Task ProcessAsync(
    CatalogImage source,
    ImageRequest request,
    string outputPath,
    CancellationToken cancellationToken)
  {
    var arguments = _argumentBuilder.Build(source, request, outputPath);
    var timeout = TimeSpan.FromSeconds(_options.ProcessTimeoutSeconds);

    await _concurrency.WaitAsync(cancellationToken);
    try
    {
      var result = await _runner.RunAsync(_options.MagickPath, arguments, timeout, cancellationToken);
      if (result.ExitCode != 0)
      {
        _logger.LogError(
          "ImageMagick failed for {Source} with exit code {ExitCode}: {Error}",
          source.Path,
          result.ExitCode,
          result.StandardError);
        throw new ImageProcessingException($"ImageMagick exited with code {result.ExitCode}.");
      }
    }
    finally
    {
      _concurrency.Release();
    }
  }

  public void Dispose() => _concurrency.Dispose();
}
