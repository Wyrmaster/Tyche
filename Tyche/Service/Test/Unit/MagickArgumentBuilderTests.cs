using Tyche.Service.Catalog;
using Tyche.Service.Formats;
using Tyche.Service.Processing;
using Tyche.Service.Requests;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class MagickArgumentBuilderTests
{
  private readonly MagickArgumentBuilder _builder = new(OptionsFactory.Create(), new ImageFormatMap());

  private static ImageRequest Request(
    int? width = null,
    int? height = null,
    ResizeMode mode = ResizeMode.Fit,
    ImageFormat? format = null)
    => new(null, width, height, 96, mode, format);

  private IReadOnlyList<string> Build(ImageFormat source, ImageRequest request)
    => _builder.Build(new CatalogImage("/img/in." + source.ToString().ToLowerInvariant(), source), request, "/cache/out.tmp");

  [Fact]
  public void Fit_with_both_sizes_only_shrinks()
  {
    var args = Build(ImageFormat.Jpg, Request(800, 600));

    Assert.Equal("800x600>", args[args.ToList().IndexOf("-resize") + 1]);
    Assert.DoesNotContain("-extent", args);
  }

  [Fact]
  public void Fit_with_one_size_omits_the_other()
  {
    Assert.Contains("800x>", Build(ImageFormat.Jpg, Request(width: 800)));
    Assert.Contains("x600>", Build(ImageFormat.Jpg, Request(height: 600)));
  }

  [Fact]
  public void Fill_resizes_to_cover_then_crops_centered()
  {
    var args = string.Join(' ', Build(ImageFormat.Png, Request(300, 200, ResizeMode.Fill)));

    Assert.Contains("-resize 300x200^ -gravity center -extent 300x200", args);
  }

  [Fact]
  public void Format_only_has_no_resize()
  {
    var args = Build(ImageFormat.Png, Request(format: ImageFormat.Webp));

    Assert.DoesNotContain("-resize", args);
    Assert.Equal("webp:/cache/out.tmp", args[^1]);
  }

  [Fact]
  public void Percent_in_the_input_path_is_escaped()
  {
    var source = new CatalogImage("/img/100%d.jpg", ImageFormat.Jpg);

    var args = _builder.Build(source, Request(100), "/cache/out.tmp");

    Assert.Contains("jpeg:/img/100%%d.jpg[0]", args);
  }

  [Fact]
  public void Doubled_percent_in_the_input_path_is_escaped_again()
  {
    var source = new CatalogImage("/img/100%%d.jpg", ImageFormat.Jpg);

    var args = _builder.Build(source, Request(100), "/cache/out.tmp");

    Assert.Contains("jpeg:/img/100%%%%d.jpg[0]", args);
  }

  [Fact]
  public void Input_and_output_use_explicit_coders_and_first_frame_for_stills()
  {
    var args = Build(ImageFormat.Jpg, Request(100));

    Assert.Contains("jpeg:/img/in.jpg[0]", args);
    Assert.Equal("jpeg:/cache/out.tmp", args[^1]);
    Assert.DoesNotContain("-coalesce", args);
  }

  [Fact]
  public void Animated_source_to_animated_target_keeps_all_frames()
  {
    var args = Build(ImageFormat.Gif, Request(100, format: ImageFormat.Webp));

    Assert.Contains("gif:/img/in.gif", args);
    Assert.Contains("-coalesce", args);
    Assert.Contains("+repage", args);
  }

  [Fact]
  public void Animated_source_to_still_target_uses_first_frame()
  {
    var args = Build(ImageFormat.Gif, Request(100, format: ImageFormat.Png));

    Assert.Contains("gif:/img/in.gif[0]", args);
    Assert.DoesNotContain("-coalesce", args);
  }

  [Fact]
  public void Jpg_target_flattens_alpha_onto_white_and_sets_quality()
  {
    var args = string.Join(' ', Build(ImageFormat.Png, Request(format: ImageFormat.Jpg)));

    Assert.Contains("-background white -alpha remove -alpha off", args);
    Assert.Contains("-quality 85", args);
  }

  [Fact]
  public void Png_target_has_no_quality_or_alpha_removal()
  {
    var args = Build(ImageFormat.Png, Request(100));

    Assert.DoesNotContain("-quality", args);
    Assert.DoesNotContain("-alpha", args);
  }

  [Fact]
  public void Always_orients_strips_and_limits_resources()
  {
    var args = string.Join(' ', Build(ImageFormat.Webp, Request(100)));

    Assert.Contains("-auto-orient -strip", args);
    Assert.Contains("-limit memory", args);
    Assert.Contains("-limit map", args);
    Assert.Contains("-limit time 30", args);
    Assert.Contains("-limit area 128MP", args);
    Assert.Contains("-limit width 32KP -limit height 32KP", args);
  }

  [Fact]
  public void Temporary_files_go_below_the_cache_folder()
  {
    var custom = new MagickArgumentBuilder(
      OptionsFactory.Create(options => options.CachePath = "/data/cache"),
      new ImageFormatMap());

    var args = custom.Build(new CatalogImage("/img/a.jpg", ImageFormat.Jpg), Request(100), "/data/cache/o.tmp");

    Assert.Contains("registry:temporary-path=/data/cache/tmp", args);
  }

  [Fact]
  public void Hostile_looking_path_stays_a_single_argument()
  {
    var source = new CatalogImage("/img/a b; rm -rf %d [x] $(id).jpg", ImageFormat.Jpg);

    var args = _builder.Build(source, Request(100), "/cache/out.tmp");

    Assert.Contains("jpeg:/img/a b; rm -rf %%d [x] $(id).jpg[0]", args);
  }
}
