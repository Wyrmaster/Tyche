namespace Tyche.Service.Test.Support;

/// <summary>A theory that is skipped with a clear message when the ImageMagick CLI is not installed.</summary>
public sealed class RequiresMagickTheoryAttribute : TheoryAttribute
{
  public RequiresMagickTheoryAttribute()
  {
    if (!TestImages.MagickAvailable && !TestImages.MagickRequired)
    {
      Skip = "ImageMagick ('magick') is not installed; install it to run this test (always runs in CI).";
    }
  }
}
