using System.Globalization;
using Microsoft.Extensions.Options;
using Tyche.Service.Caching;
using Tyche.Service.Catalog;
using Tyche.Service.Configuration;
using Tyche.Service.Formats;
using Tyche.Service.Requests;

namespace Tyche.Service.Processing;

public sealed class MagickArgumentBuilder : IMagickArgumentBuilder
{
  private const int Quality = 85;

  private readonly ImageApiOptions options;
  private readonly IImageFormatMap formats;

  public MagickArgumentBuilder(IOptions<ImageApiOptions> options, IImageFormatMap formats)
  {
    this.options = options.Value;
    this.formats = formats;
  }

  public IReadOnlyList<string> Build(CatalogImage source, ImageRequest request, string outputPath)
  {
    var target = request.Format ?? source.Format;
    var animated = formats.CanAnimate(source.Format) && formats.CanAnimate(target);

    var arguments = new List<string>
    {
      "-limit", "memory", "256MiB",
      "-limit", "map", "512MiB",
      "-limit", "disk", "2GiB",
      "-limit", "area", "128MP",
      "-limit", "width", "32KP",
      "-limit", "height", "32KP",
      "-define", $"registry:temporary-path={DiskImageCache.MagickTemporaryFolder(options.CachePath)}",
      "-limit", "time", options.ProcessTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
    };

    // The explicit coder prefix makes ImageMagick read the file as the format its extension claims,
    // instead of guessing from the content. [0] selects the first frame unless the animation is kept.
    // A literal % is doubled, otherwise ImageMagick reads a name like 100%d.png as a scene pattern.
    var input = $"{formats.GetMagickCoder(source.Format)}:{source.Path.Replace("%", "%%")}";
    arguments.Add(animated ? input : $"{input}[0]");

    if (animated)
    {
      arguments.Add("-coalesce");
    }

    arguments.Add("-auto-orient");
    arguments.Add("-strip");

    if (target == ImageFormat.Jpg)
    {
      arguments.AddRange(["-background", "white", "-alpha", "remove", "-alpha", "off"]);
    }

    if (request.Width is not null || request.Height is not null)
    {
      AddResize(arguments, request);
    }

    AddOutputOptions(arguments, target, animated);

    arguments.Add($"{formats.GetMagickCoder(target)}:{outputPath}");
    return arguments;
  }

  private static void AddOutputOptions(List<string> arguments, ImageFormat target, bool animated)
  {
    if (animated)
    {
      arguments.Add("+repage");
      if (target == ImageFormat.Gif)
      {
        arguments.AddRange(["-layers", "optimize"]);
      }
    }

    if (target is ImageFormat.Jpg or ImageFormat.Webp)
    {
      arguments.AddRange(["-quality", Quality.ToString(CultureInfo.InvariantCulture)]);
    }
  }

  private static void AddResize(List<string> arguments, ImageRequest request)
  {
    var width = request.Width?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    var height = request.Height?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    if (request.Mode == ResizeMode.Fill)
    {
      arguments.AddRange(["-resize", $"{width}x{height}^", "-gravity", "center", "-extent", $"{width}x{height}"]);
    }
    else
    {
      // '>' only shrinks, so fit never upscales.
      arguments.AddRange(["-resize", $"{width}x{height}>"]);
    }
  }
}
