using System.Net;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public class ResizeAndConvertTests : IntegrationTestBase
{
  private void Single(string name, int width, int height)
  {
    TestImages.Create(Path.Combine(Nature, name), width, height);
    Start();
  }

  private async Task<(string Format, int Width, int Height, int Frames)> Fetch(string query, string expectedType)
  {
    var (response, body) = await GetAsync("/image?" + query);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal(expectedType, response.Content.Headers.ContentType!.MediaType);
    Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    return TestImages.Identify(body);
  }

  [RequiresMagickFact]
  public async Task Fit_with_both_sizes_keeps_aspect_ratio_inside_the_box()
  {
    Single("a.jpg", 1600, 900);

    var result = await Fetch("width=800&height=600", "image/jpeg");

    Assert.Equal(("JPEG", 800, 450), (result.Format, result.Width, result.Height));
  }

  [RequiresMagickFact]
  public async Task Fit_with_only_width_or_only_height_keeps_aspect_ratio()
  {
    Single("a.png", 1000, 500);

    var byWidth = await Fetch("width=200", "image/png");
    var byHeight = await Fetch("height=100", "image/png");

    Assert.Equal((200, 100), (byWidth.Width, byWidth.Height));
    Assert.Equal((200, 100), (byHeight.Width, byHeight.Height));
  }

  [RequiresMagickFact]
  public async Task Fill_returns_exactly_the_requested_size()
  {
    Single("a.png", 1000, 500);

    var wide = await Fetch("mode=fill&width=300&height=300", "image/png");
    var tall = await Fetch("mode=fill&width=100&height=400", "image/png");
    var upscaled = await Fetch("mode=fill&width=2000&height=2000", "image/png");

    Assert.Equal((300, 300), (wide.Width, wide.Height));
    Assert.Equal((100, 400), (tall.Width, tall.Height));
    Assert.Equal((2000, 2000), (upscaled.Width, upscaled.Height));
  }

  [RequiresMagickFact]
  public async Task Dpi_192_doubles_the_output_size()
  {
    Single("a.png", 2000, 1000);

    var normal = await Fetch("width=400&height=400", "image/png");
    var doubled = await Fetch("width=400&height=400&dpi=192", "image/png");
    var fill = await Fetch("mode=fill&width=100&height=50&dpi=192", "image/png");

    Assert.Equal((400, 200), (normal.Width, normal.Height));
    Assert.Equal((800, 400), (doubled.Width, doubled.Height));
    Assert.Equal((200, 100), (fill.Width, fill.Height));
  }

  [RequiresMagickFact]
  public async Task Fit_never_upscales()
  {
    Single("small.png", 100, 50);

    var result = await Fetch("width=800&height=600", "image/png");
    var dpi = await Fetch("width=800&dpi=192", "image/png");

    Assert.Equal((100, 50), (result.Width, result.Height));
    Assert.Equal((100, 50), (dpi.Width, dpi.Height));
  }

  [RequiresMagickFact]
  public async Task Format_conversion_alone_keeps_the_size()
  {
    Single("a.png", 120, 80);

    var webp = await Fetch("format=webp", "image/webp");
    var jpg = await Fetch("format=jpg", "image/jpeg");
    var gif = await Fetch("format=gif", "image/gif");
    var jpeg = await Fetch("format=jpeg", "image/jpeg");

    Assert.Equal(("WEBP", 120, 80), (webp.Format, webp.Width, webp.Height));
    Assert.Equal(("JPEG", 120, 80), (jpg.Format, jpg.Width, jpg.Height));
    Assert.Equal(("GIF", 120, 80), (gif.Format, gif.Width, gif.Height));
    Assert.Equal("JPEG", jpeg.Format);
  }

  [RequiresMagickFact]
  public async Task Without_format_the_source_format_is_kept_after_resize()
  {
    Single("a.webp", 400, 200);

    var result = await Fetch("width=100", "image/webp");

    Assert.Equal(("WEBP", 100, 50), (result.Format, result.Width, result.Height));
  }

  [RequiresMagickFact]
  public async Task Format_and_resize_combine()
  {
    Single("a.png", 1000, 500);

    var result = await Fetch("width=200&height=200&format=webp", "image/webp");
    var fill = await Fetch("mode=fill&width=64&height=64&format=jpg&dpi=96", "image/jpeg");

    Assert.Equal(("WEBP", 200, 100), (result.Format, result.Width, result.Height));
    Assert.Equal(("JPEG", 64, 64), (fill.Format, fill.Width, fill.Height));
  }

  [RequiresMagickFact]
  public async Task Animated_gif_is_resized_and_keeps_its_frames()
  {
    TestImages.CreateAnimatedGif(Path.Combine(Nature, "anim.gif"), 300, 200);
    Start();

    var gif = await Fetch("width=150", "image/gif");
    var webp = await Fetch("width=150&format=webp", "image/webp");
    var still = await Fetch("width=150&format=png", "image/png");

    Assert.Equal((150, 100, 2), (gif.Width, gif.Height, gif.Frames));
    Assert.Equal((150, 100), (webp.Width, webp.Height));
    Assert.True(webp.Frames == 2 || webp.Frames == 1, "animated webp may fall back to a single frame");
    Assert.Equal((150, 100, 1), (still.Width, still.Height, still.Frames));
  }

  [RequiresMagickFact]
  public async Task Transparent_png_becomes_a_valid_jpg_on_white()
  {
    TestImages.CreateTransparentPng(Path.Combine(Nature, "t.png"), 50, 50);
    Start();

    var result = await Fetch("format=jpg", "image/jpeg");

    Assert.Equal(("JPEG", 50, 50), (result.Format, result.Width, result.Height));
  }

  [RequiresMagickFact]
  public async Task File_name_with_special_characters_is_handled()
  {
    TestImages.Create(Path.Combine(Nature, "we[ird] na;me $(id) 100%d.png"), 200, 100);
    Start();

    var result = await Fetch("width=100", "image/png");

    Assert.Equal((100, 50), (result.Width, result.Height));
  }

  [RequiresMagickFact]
  public async Task Content_that_does_not_match_the_extension_is_not_processed()
  {
    // A text/MVG file hiding behind .jpg must not be interpreted by another ImageMagick coder.
    File.WriteAllText(Path.Combine(Nature, "evil.jpg"), "push graphic-context\nviewbox 0 0 10 10\nfill red\nrectangle 0,0 5,5\npop graphic-context\n");
    Start();

    var (response, _) = await GetAsync("/image?width=5");

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
  }

  [RequiresMagickFact]
  public async Task Corrupt_image_gives_generic_500_and_leaves_no_cache_file()
  {
    File.WriteAllBytes(Path.Combine(Nature, "broken.png"), [1, 2, 3, 4]);
    Start();

    var (response, body) = await GetAsync("/image?width=10");
    var text = System.Text.Encoding.UTF8.GetString(body);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Contains("could not be processed", text);
    Assert.DoesNotContain("broken.png", text);
    Assert.DoesNotContain("magick", text, StringComparison.OrdinalIgnoreCase);
    Assert.Empty(Directory.GetFiles(Factory.CachePath));
  }

  [Fact]
  public async Task Unusable_magick_gives_500_for_conversions()
  {
    File.WriteAllBytes(Path.Combine(Nature, "a.png"), [1, 2, 3]);
    Prepare(("nature", Nature)).With("MagickPath", "/no/such/magick");
    Start();

    var (response, body) = await GetAsync("/image?width=10");

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.DoesNotContain("/no/such", System.Text.Encoding.UTF8.GetString(body));
    Assert.Empty(Directory.GetFiles(Factory.CachePath));
  }
}
