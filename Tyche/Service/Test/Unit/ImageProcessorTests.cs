using Microsoft.Extensions.Logging.Abstractions;
using Tyche.Service.Catalog;
using Tyche.Service.Formats;
using Tyche.Service.Processing;
using Tyche.Service.Requests;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class ImageProcessorTests
{
  private static readonly CatalogImage Source = new("/img/a.jpg", ImageFormat.Jpg);
  private static readonly ImageRequest Request = new(null, 100, null, 96, ResizeMode.Fit, null);

  private static ImageProcessor Create(FakeProcessRunner runner, int concurrency)
  {
    var options = OptionsFactory.Create(o => o.MaxConcurrentProcesses = concurrency);
    return new ImageProcessor(
      options,
      new MagickArgumentBuilder(options, new ImageFormatMap()),
      runner,
      NullLogger<ImageProcessor>.Instance);
  }

  [Fact]
  public async Task Concurrent_runs_are_limited()
  {
    var runner = new FakeProcessRunner { Delay = TimeSpan.FromMilliseconds(100) };
    var processor = Create(runner, concurrency: 2);

    await Task.WhenAll(Enumerable
      .Range(0, 8)
      .Select(_ => processor.ProcessAsync(Source, Request, "/cache/x.tmp", CancellationToken.None)));

    Assert.Equal(8, runner.Calls.Count);
    Assert.Equal(2, runner.MaxConcurrent);
  }

  [Fact]
  public async Task Non_zero_exit_becomes_processing_exception_without_details()
  {
    var processor = Create(new FakeProcessRunner { ExitCode = 1 }, concurrency: 1);

    var exception = await Assert.ThrowsAsync<ImageProcessingException>(
      () => processor.ProcessAsync(Source, Request, "/cache/x.tmp", CancellationToken.None));

    Assert.DoesNotContain("boom", exception.Message);
  }

  [Fact]
  public async Task Slot_is_released_after_a_failure()
  {
    var runner = new FakeProcessRunner { ExitCode = 1 };
    var processor = Create(runner, concurrency: 1);

    for (var i = 0; i < 3; i++)
    {
      await Assert.ThrowsAsync<ImageProcessingException>(
        () => processor.ProcessAsync(Source, Request, "/cache/x.tmp", CancellationToken.None));
    }

    Assert.Equal(3, runner.Calls.Count);
  }
}
