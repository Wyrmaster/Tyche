using System.Net;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public class CacheIntegrationTests : IntegrationTestBase
{
  [RequiresMagickFact]
  public async Task Same_parameters_are_served_from_the_cache_the_second_time()
  {
    TestImages.Create(Path.Combine(Nature, "a.png"), 400, 300);
    Start();

    var first = await GetAsync("/image?width=100&format=webp");
    var second = await GetAsync("/image?width=100&format=webp");

    Assert.Equal(1, Factory.Runner.Conversions);
    Assert.Equal(first.Body, second.Body);
    Assert.Single(Directory.GetFiles(Factory.CachePath));
  }

  [RequiresMagickFact]
  public async Task Different_parameters_are_separate_cache_entries()
  {
    TestImages.Create(Path.Combine(Nature, "a.png"), 400, 300);
    Start();

    await GetAsync("/image?width=100");
    await GetAsync("/image?width=101");
    await GetAsync("/image?width=100&mode=fill&height=100");
    await GetAsync("/image?width=100&format=jpg");
    await GetAsync("/image?width=100&dpi=96");

    Assert.Equal(4, Factory.Runner.Conversions);
    Assert.Equal(4, Directory.GetFiles(Factory.CachePath).Length);
  }

  [RequiresMagickFact]
  public async Task Changed_source_file_does_not_return_the_stale_result()
  {
    var path = TestImages.Create(Path.Combine(Nature, "a.png"), 400, 300);
    Start();
    var before = TestImages.Identify((await GetAsync("/image?width=100")).Body);

    TestImages.Create(path, 400, 100, "blue");
    File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
    var after = TestImages.Identify((await GetAsync("/image?width=100")).Body);

    Assert.Equal((100, 75), (before.Width, before.Height));
    Assert.Equal((100, 25), (after.Width, after.Height));
    Assert.Equal(2, Factory.Runner.Conversions);
  }

  [RequiresMagickFact]
  public async Task Parallel_identical_requests_convert_once_and_all_succeed()
  {
    TestImages.Create(Path.Combine(Nature, "a.png"), 1200, 800);
    Start();

    var responses = await Task.WhenAll(Enumerable
      .Range(0, 12)
      .Select(_ => GetAsync("/image?width=300&format=jpg")));

    Assert.All(responses, result => Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode));
    Assert.All(responses, result => Assert.Equal(responses[0].Body, result.Body));
    Assert.Equal(1, Factory.Runner.Conversions);
    Assert.Empty(Directory.GetFiles(Factory.CachePath, "*.tmp"));
  }

  [RequiresMagickFact]
  public async Task Cache_size_limit_keeps_the_folder_small()
  {
    for (var i = 0; i < 6; i++)
    {
      TestImages.Create(Path.Combine(Nature, $"{i}.png"), 600, 600, i % 2 == 0 ? "plasma:red-blue" : "plasma:green-white");
    }

    Prepare(("nature", Nature)).With("CacheMaxSizeMb", "1");
    Start();

    for (var width = 500; width < 560; width += 5)
    {
      await GetAsync($"/image?width={width}&format=jpg");
    }

    var total = Directory.GetFiles(Factory.CachePath).Sum(file => new FileInfo(file).Length);
    Assert.True(total <= 1024 * 1024 + 300 * 1024, $"cache is {total} bytes");
  }

  [RequiresMagickFact]
  public async Task Original_files_are_never_cached()
  {
    TestImages.Create(Path.Combine(Nature, "a.png"), 40, 30);
    Start();

    await GetAsync("/image");
    await GetAsync("/image?dpi=192");

    Assert.Empty(Directory.GetFiles(Factory.CachePath));
  }
}
