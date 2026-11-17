using Tyche.Service.Formats;
using Tyche.Service.Requests;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class ImageRequestParserTests
{
  private readonly ImageRequestParser _parser = new(OptionsFactory.Create(), new ImageFormatMap());

  [Fact]
  public void No_parameters_is_a_pass_through_request()
  {
    var result = _parser.Parse(null, null, null, null, null, null);

    Assert.True(result.IsValid);
    Assert.True(result.Request!.IsPassThrough);
    Assert.Equal(96, result.Request.Dpi);
    Assert.Equal(ResizeMode.Fit, result.Request.Mode);
  }

  [Fact]
  public void Dpi_and_mode_alone_stay_pass_through()
  {
    Assert.True(_parser.Parse(null, null, null, "192", "fit", null).Request!.IsPassThrough);
  }

  [Fact]
  public void Format_alone_is_not_pass_through()
  {
    var request = _parser.Parse(null, null, null, null, null, "webp").Request!;

    Assert.False(request.IsPassThrough);
    Assert.Equal(ImageFormat.Webp, request.Format);
  }

  [Theory]
  [InlineData("800", "600", null, 800, 600)]
  [InlineData("800", "600", "192", 1600, 1200)]
  [InlineData("100", null, "144", 150, null)]
  [InlineData(null, "101", "48", null, 51)]
  [InlineData("1", "1", "96", 1, 1)]
  public void Sizes_are_scaled_by_dpi_and_rounded(string? width, string? height, string? dpi, int? w, int? h)
  {
    var request = _parser.Parse(null, width, height, dpi, null, null).Request!;

    Assert.Equal(w, request.Width);
    Assert.Equal(h, request.Height);
  }

  [Theory]
  [InlineData("width", "abc")]
  [InlineData("width", "0")]
  [InlineData("width", "-5")]
  [InlineData("width", "1.5")]
  [InlineData("width", "+5")]
  [InlineData("width", " 5")]
  [InlineData("width", "99999999999999999999")]
  [InlineData("height", "x")]
  [InlineData("dpi", "0")]
  [InlineData("dpi", "fast")]
  public void Non_numeric_or_non_positive_values_are_rejected(string name, string value)
  {
    var result = _parser.Parse(
      null,
      name == "width" ? value : "100",
      name == "height" ? value : "100",
      name == "dpi" ? value : null,
      null,
      null);

    Assert.False(result.IsValid);
    Assert.Contains(name, result.Problems.Keys);
  }

  [Theory]
  [InlineData("8192", null, null, true)]
  [InlineData("8193", null, null, false)]
  [InlineData(null, "8193", null, false)]
  [InlineData(null, "8192", null, true)]
  [InlineData("4097", null, "192", false)]
  [InlineData("4096", null, "192", true)]
  [InlineData("100", "10", "8193", false)]
  [InlineData("2147483647", null, null, false)]
  [InlineData("2147483647", "2147483647", "2147483647", false)]
  public void Scaled_size_above_the_maximum_is_rejected(string? width, string? height, string? dpi, bool valid)
  {
    Assert.Equal(valid, _parser.Parse(null, width, height, dpi, null, null).IsValid);
  }

  [Fact]
  public void Custom_maximum_is_honoured()
  {
    var small = new ImageRequestParser(OptionsFactory.Create(options => options.MaxDimension = 100), new ImageFormatMap());

    Assert.True(small.Parse(null, "100", null, null, null, null).IsValid);
    Assert.False(small.Parse(null, "101", null, null, null, null).IsValid);
  }

  [Fact]
  public void Size_that_scales_below_one_pixel_is_rejected()
  {
    Assert.False(_parser.Parse(null, "1", null, "1", null, null).IsValid);
  }

  [Theory]
  [InlineData("fill", "100", null)]
  [InlineData("fill", null, "100")]
  [InlineData("fill", null, null)]
  [InlineData("crop", "100", "100")]
  [InlineData("", "100", "100", true)]
  public void Mode_rules(string mode, string? width, string? height, bool valid = false)
  {
    Assert.Equal(valid, _parser.Parse(null, width, height, null, mode, null).IsValid);
  }

  [Fact]
  public void Fill_with_both_sizes_is_valid_and_mode_is_case_insensitive()
  {
    var request = _parser.Parse(null, "300", "200", null, "FILL", null).Request!;

    Assert.Equal(ResizeMode.Fill, request.Mode);
    Assert.Equal((300, 200), (request.Width, request.Height));
  }

  [Theory]
  [InlineData("jpg", ImageFormat.Jpg)]
  [InlineData("JPEG", ImageFormat.Jpg)]
  [InlineData("png", ImageFormat.Png)]
  [InlineData("webp", ImageFormat.Webp)]
  [InlineData("gif", ImageFormat.Gif)]
  public void Supported_formats(string format, ImageFormat expected)
  {
    Assert.Equal(expected, _parser.Parse(null, null, null, null, null, format).Request!.Format);
  }

  [Theory]
  [InlineData("bmp")]
  [InlineData("avif")]
  [InlineData("../x")]
  [InlineData("png;rm")]
  public void Unsupported_format_is_rejected(string format)
  {
    var result = _parser.Parse(null, null, null, null, null, format);

    Assert.False(result.IsValid);
    Assert.Contains("format", result.Problems.Keys);
  }

  [Fact]
  public void Several_problems_are_reported_together()
  {
    var result = _parser.Parse(null, "x", "y", null, "zoom", "bmp");

    Assert.Equal(4, result.Problems.Count);
  }

  [Fact]
  public void Collection_is_passed_through_as_name_only()
  {
    Assert.Equal("nature", _parser.Parse("nature", null, null, null, null, null).Request!.Collection);
    Assert.Null(_parser.Parse("", null, null, null, null, null).Request!.Collection);
  }
}
