using System.Net;
using System.Text.Json;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public class ImageEndpointTests : IntegrationTestBase
{
  [Fact]
  public async Task Original_is_returned_unchanged_without_calling_magick()
  {
    var path = TestImages.Create(Path.Combine(Nature, "a.png"), 40, 30);
    Start();

    var (response, body) = await GetAsync("/image");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
    Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    Assert.Equal(await File.ReadAllBytesAsync(path), body);
    Assert.Equal(0, Factory.Runner.Conversions);
  }

  [Fact]
  public async Task Pass_through_works_without_magick_even_for_a_dummy_file()
  {
    File.WriteAllBytes(Path.Combine(Nature, "x.jpg"), [1, 2, 3]);
    Prepare(("nature", Nature)).With("MagickPath", "/no/such/magick");
    Start();

    var (response, body) = await GetAsync("/image?dpi=192&mode=fit");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
    Assert.Equal(new byte[] { 1, 2, 3 }, body);
  }

  [Fact]
  public async Task Every_file_of_the_collection_is_eventually_served()
  {
    for (var i = 0; i < 4; i++)
    {
      File.WriteAllBytes(Path.Combine(Nature, $"{i}.png"), [(byte)i]);
    }

    Start();
    var seen = new HashSet<byte>();
    for (var i = 0; i < 80; i++)
    {
      seen.Add((await GetAsync("/image")).Body[0]);
    }

    Assert.Equal(4, seen.Count);
  }

  [Fact]
  public async Task Empty_collection_is_404_with_problem_details()
  {
    File.WriteAllText(Path.Combine(Nature, "readme.txt"), "no image");
    Start();

    var (response, body) = await GetAsync("/image");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    Assert.Contains("contains no images", JsonDocument.Parse(body).RootElement.GetProperty("detail").GetString());
  }

  [Fact]
  public async Task Named_and_default_collections_and_unknown_name()
  {
    var city = Temp.Sub("city");
    File.WriteAllBytes(Path.Combine(Nature, "n.png"), [1]);
    File.WriteAllBytes(Path.Combine(city, "c.png"), [2]);
    Start(("nature", Nature), ("city", city));

    Assert.Equal(new byte[] { 1 }, (await GetAsync("/image")).Body);
    Assert.Equal(new byte[] { 1 }, (await GetAsync("/image?collection=nature")).Body);
    Assert.Equal(new byte[] { 2 }, (await GetAsync("/image?collection=city")).Body);
    Assert.Equal(HttpStatusCode.NotFound, (await GetAsync("/image?collection=nope")).Response.StatusCode);
  }

  [Fact]
  public async Task Subfolders_are_included_and_other_types_ignored()
  {
    Directory.CreateDirectory(Path.Combine(Nature, "deep"));
    File.WriteAllBytes(Path.Combine(Nature, "deep", "only.GIF"), [7]);
    File.WriteAllBytes(Path.Combine(Nature, "ignored.bmp"), [8]);
    File.WriteAllBytes(Path.Combine(Nature, "ignored.txt"), [9]);
    Start();

    for (var i = 0; i < 10; i++)
    {
      var (response, body) = await GetAsync("/image");
      Assert.Equal("image/gif", response.Content.Headers.ContentType!.MediaType);
      Assert.Equal(new byte[] { 7 }, body);
    }
  }

  [Fact]
  public async Task Added_and_removed_files_show_up_without_restart()
  {
    var first = Path.Combine(Nature, "first.png");
    File.WriteAllBytes(first, [1]);
    Start();
    Assert.Equal(new byte[] { 1 }, (await GetAsync("/image")).Body);

    File.WriteAllBytes(Path.Combine(Nature, "second.png"), [2]);
    File.Delete(first);
    await Task.Delay(1300);

    for (var i = 0; i < 5; i++)
    {
      Assert.Equal(new byte[] { 2 }, (await GetAsync("/image")).Body);
    }
  }

  [Fact]
  public async Task File_removed_within_the_listing_time_is_skipped_not_500()
  {
    var gone = Path.Combine(Nature, "gone.png");
    File.WriteAllBytes(gone, [1]);
    File.WriteAllBytes(Path.Combine(Nature, "stay.png"), [2]);
    Prepare(("nature", Nature)).With("ListingCacheSeconds", "600");
    Start();
    await GetAsync("/image");
    File.Delete(gone);

    for (var i = 0; i < 20; i++)
    {
      var (response, body) = await GetAsync("/image");
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      Assert.Equal(new byte[] { 2 }, body);
    }
  }

  [Theory]
  [InlineData("collection=../")]
  [InlineData("collection=..%2F..%2Fetc")]
  [InlineData("collection=%2Fetc")]
  [InlineData("collection=/etc/passwd")]
  [InlineData("collection=nature/..")]
  [InlineData("collection=nature%00")]
  [InlineData("collection=C:%5Cwindows")]
  public async Task Path_like_collection_names_never_reach_the_file_system(string query)
  {
    File.WriteAllBytes(Path.Combine(Nature, "n.png"), [1]);
    Start();

    var (response, _) = await GetAsync("/image?" + query);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Theory]
  [InlineData("file=/etc/passwd")]
  [InlineData("path=../../etc/passwd")]
  [InlineData("name=../secret.png")]
  public async Task Unknown_parameters_that_look_like_paths_are_ignored(string query)
  {
    File.WriteAllBytes(Path.Combine(Nature, "n.png"), [1]);
    Start();

    var (response, body) = await GetAsync("/image?" + query);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(new byte[] { 1 }, body);
  }

  [Fact]
  public async Task Path_traversal_in_the_url_path_does_not_serve_files()
  {
    File.WriteAllBytes(Path.Combine(Nature, "n.png"), [1]);
    Start();

    foreach (var url in new[] { "/image/../../etc/passwd", "/%2e%2e/etc/passwd", "/images/n.png", "/n.png" })
    {
      Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(url)).Response.StatusCode);
    }
  }

  [Fact]
  public async Task Invalid_configuration_fails_at_startup_with_a_clear_message()
  {
    Prepare(("nature", Temp.Combine("missing")));

    var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

    Assert.Contains("missing", exception.ToString());
    Assert.Contains("does not exist", exception.ToString());
  }

  [Fact]
  public async Task Missing_collections_fail_at_startup()
  {
    Prepare();

    var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

    Assert.Contains("Collections is empty", exception.ToString());
    await Task.CompletedTask;
  }

  [Theory]
  [InlineData("width=abc")]
  [InlineData("width=0")]
  [InlineData("width=-1")]
  [InlineData("height=1.5")]
  [InlineData("dpi=0")]
  [InlineData("mode=fill")]
  [InlineData("mode=fill&width=100")]
  [InlineData("mode=crop&width=100&height=100")]
  [InlineData("width=8193")]
  [InlineData("width=4097&dpi=192")]
  [InlineData("format=bmp")]
  [InlineData("format=png%3Bls")]
  [InlineData("width=99999999999999999999")]
  public async Task Invalid_parameters_return_400_with_reason(string query)
  {
    File.WriteAllBytes(Path.Combine(Nature, "n.png"), [1]);
    Start();

    var (response, body) = await GetAsync("/image?" + query);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    Assert.True(JsonDocument
      .Parse(body)
      .RootElement
      .GetProperty("errors")
      .EnumerateObject()
      .Any());
    Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    Assert.Equal(0, Factory.Runner.Conversions);
  }
}
