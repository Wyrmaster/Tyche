using Tyche.Service.Formats;

namespace Tyche.Service.Test.Unit;

public class ImageFormatMapTests
{
    private readonly ImageFormatMap map = new();

    [Theory]
    [InlineData("jpg", ImageFormat.Jpg)]
    [InlineData(".JPEG", ImageFormat.Jpg)]
    [InlineData("Png", ImageFormat.Png)]
    [InlineData(".webp", ImageFormat.Webp)]
    [InlineData("GIF", ImageFormat.Gif)]
    public void Extension_maps_case_insensitive(string extension, ImageFormat expected)
    {
        Assert.True(map.TryFromExtension(extension, out var format));
        Assert.Equal(expected, format);
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("")]
    [InlineData("txt")]
    public void Unknown_extension_is_rejected(string extension)
    {
        Assert.False(map.TryFromExtension(extension, out _));
    }

    [Theory]
    [InlineData(ImageFormat.Jpg, "image/jpeg")]
    [InlineData(ImageFormat.Png, "image/png")]
    [InlineData(ImageFormat.Webp, "image/webp")]
    [InlineData(ImageFormat.Gif, "image/gif")]
    public void Content_types(ImageFormat format, string expected)
    {
        Assert.Equal(expected, map.GetContentType(format));
    }

    [Fact]
    public void Only_gif_and_webp_animate()
    {
        Assert.True(map.CanAnimate(ImageFormat.Gif));
        Assert.True(map.CanAnimate(ImageFormat.Webp));
        Assert.False(map.CanAnimate(ImageFormat.Jpg));
        Assert.False(map.CanAnimate(ImageFormat.Png));
    }
}
