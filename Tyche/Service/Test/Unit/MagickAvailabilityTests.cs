using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class MagickAvailabilityTests
{
  [Fact]
  public void Magick_is_installed_when_the_environment_requires_it()
  {
    if (TestImages.MagickRequired)
    {
      Assert.True(TestImages.MagickAvailable, "REQUIRE_MAGICK=1 or CI=true, but 'magick' cannot be started.");
    }
  }
}
