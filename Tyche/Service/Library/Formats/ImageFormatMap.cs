namespace Tyche.Service.Formats;

public sealed class ImageFormatMap : IImageFormatMap
{
  private static readonly Dictionary<string, ImageFormat> Extensions = new(StringComparer.OrdinalIgnoreCase)
  {
    ["jpg"] = ImageFormat.Jpg,
    ["jpeg"] = ImageFormat.Jpg,
    ["png"] = ImageFormat.Png,
    ["webp"] = ImageFormat.Webp,
    ["gif"] = ImageFormat.Gif,
  };

  public IReadOnlyCollection<string> SupportedExtensions => Extensions.Keys;

  public bool TryFromExtension(string extension, out ImageFormat format)
    => Extensions.TryGetValue(extension.TrimStart('.'), out format);

  public bool TryParse(string name, out ImageFormat format)
    => Extensions.TryGetValue(name, out format);

  public string GetContentType(ImageFormat format) => format switch
  {
    ImageFormat.Jpg => "image/jpeg",
    ImageFormat.Png => "image/png",
    ImageFormat.Webp => "image/webp",
    ImageFormat.Gif => "image/gif",
    _ => throw new ArgumentOutOfRangeException(nameof(format)),
  };

  public string GetMagickCoder(ImageFormat format) => format switch
  {
    ImageFormat.Jpg => "jpeg",
    ImageFormat.Png => "png",
    ImageFormat.Webp => "webp",
    ImageFormat.Gif => "gif",
    _ => throw new ArgumentOutOfRangeException(nameof(format)),
  };

  public bool CanAnimate(ImageFormat format) => format is ImageFormat.Gif or ImageFormat.Webp;
}
