namespace Tyche.Service.Test.Support;

/// <summary>A fact that is skipped with a clear message when the ImageMagick CLI is not installed.</summary>
public sealed class RequiresMagickFactAttribute : FactAttribute
{
  public RequiresMagickFactAttribute()
  {
    // In CI (or with REQUIRE_MAGICK=1) a missing ImageMagick must fail instead of skipping, so the test
    // runs and fails (see MagickAvailabilityTests).
    if (!TestImages.MagickAvailable && !TestImages.MagickRequired)
    {
      Skip = "ImageMagick ('magick') is not installed; install it to run this test (always runs in CI).";
    }
  }
}
