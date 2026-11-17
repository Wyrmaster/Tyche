using Tyche.Service.Formats;

namespace Tyche.Service.Test.Unit;

public class ImageFormatMapTests
{
  private readonly ImageFormatMap _map = new();

  [Theory]
  [InlineData("jpg", ImageFormat.Jpg)]
  [InlineData(".JPEG", ImageFormat.Jpg)]
  [InlineData("Png", ImageFormat.Png)]
  [InlineData(".webp", ImageFormat.Webp)]
  [InlineData("GIF", ImageFormat.Gif)]
  public void Extension_maps_case_insensitive(string extension, ImageFormat expected)
  {
    Assert.True(_map.TryFromExtension(extension, out var format));
    Assert.Equal(expected, format);
  }

  [Theory]
  [InlineData("bmp")]
  [InlineData("")]
  [InlineData("txt")]
  public void Unknown_extension_is_rejected(string extension)
  {
    Assert.False(_map.TryFromExtension(extension, out _));
  }

  [Theory]
  [InlineData(ImageFormat.Jpg, "image/jpeg")]
  [InlineData(ImageFormat.Png, "image/png")]
  [InlineData(ImageFormat.Webp, "image/webp")]
  [InlineData(ImageFormat.Gif, "image/gif")]
  public void Content_types(ImageFormat format, string expected)
  {
    Assert.Equal(expected, _map.GetContentType(format));
  }

  [Fact]
  public void Only_gif_and_webp_animate()
  {
    Assert.True(_map.CanAnimate(ImageFormat.Gif));
    Assert.True(_map.CanAnimate(ImageFormat.Webp));
    Assert.False(_map.CanAnimate(ImageFormat.Jpg));
    Assert.False(_map.CanAnimate(ImageFormat.Png));
  }
}
