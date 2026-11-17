using System.Globalization;
using Microsoft.Extensions.Options;
using Tyche.Service.Configuration;
using Tyche.Service.Formats;

namespace Tyche.Service.Requests;

public sealed class ImageRequestParser : IImageRequestParser
{
  private const int BaseDpi = 96;

  private readonly ImageApiOptions _options;
  private readonly IImageFormatMap _formats;

  public ImageRequestParser(IOptions<ImageApiOptions> options, IImageFormatMap formats)
  {
    _options = options.Value;
    _formats = formats;
  }

  public ImageRequestParseResult Parse(
    string? collection,
    string? width,
    string? height,
    string? dpi,
    string? mode,
    string? format)
  {
    var problems = new Dictionary<string, string[]>();

    var parsedWidth = ParsePositive(width, "width", problems);
    var parsedHeight = ParsePositive(height, "height", problems);
    var parsedDpi = ParsePositive(dpi, "dpi", problems) ?? BaseDpi;

    var parsedMode = ParseMode(mode, problems);
    var parsedFormat = ParseFormat(format, problems);

    if (parsedMode == ResizeMode.Fill && !problems.ContainsKey("mode")
      && (string.IsNullOrEmpty(width) || string.IsNullOrEmpty(height)))
    {
      problems["mode"] = ["mode=fill needs both width and height."];
    }

    var outputWidth = Scale(parsedWidth, parsedDpi, "width", problems);
    var outputHeight = Scale(parsedHeight, parsedDpi, "height", problems);

    if (problems.Count > 0)
    {
      return new ImageRequestParseResult(null, problems);
    }

    var collectionName = string.IsNullOrEmpty(collection) ? null : collection;
    var request = new ImageRequest(collectionName, outputWidth, outputHeight, parsedDpi, parsedMode, parsedFormat);
    return new ImageRequestParseResult(request, problems);
  }

  private static ResizeMode ParseMode(string? mode, Dictionary<string, string[]> problems)
  {
    if (string.IsNullOrEmpty(mode) || mode.Equals("fit", StringComparison.OrdinalIgnoreCase))
    {
      return ResizeMode.Fit;
    }

    if (mode.Equals("fill", StringComparison.OrdinalIgnoreCase))
    {
      return ResizeMode.Fill;
    }

    problems["mode"] = ["mode must be 'fit' or 'fill'."];
    return ResizeMode.Fit;
  }

  private ImageFormat? ParseFormat(string? format, Dictionary<string, string[]> problems)
  {
    if (string.IsNullOrEmpty(format))
    {
      return null;
    }

    if (_formats.TryParse(format, out var known))
    {
      return known;
    }

    problems["format"] = ["format must be one of jpg, png, webp, gif."];
    return null;
  }

  private static int? ParsePositive(string? value, string name, Dictionary<string, string[]> problems)
  {
    if (string.IsNullOrEmpty(value))
    {
      return null;
    }

    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
    {
      problems[name] = [$"{name} must be a positive whole number."];
      return null;
    }

    return number;
  }

  private int? Scale(int? size, int dpi, string name, Dictionary<string, string[]> problems)
  {
    if (size is null || problems.ContainsKey(name))
    {
      return null;
    }

    var scaled = (long)Math.Round((double)size.Value * dpi / BaseDpi, MidpointRounding.AwayFromZero);
    if (scaled < 1)
    {
      problems[name] = [$"{name} scaled by dpi is smaller than 1 pixel."];
      return null;
    }

    if (scaled > _options.MaxDimension)
    {
      problems[name] = [$"{name} scaled by dpi is {scaled} pixels; the maximum is {_options.MaxDimension}."];
      return null;
    }

    return (int)scaled;
  }
}
