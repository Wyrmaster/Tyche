using Tyche.Service.Versioning;

namespace Tyche.Service.Test.Unit;

public class VersionProviderTests
{
  [Theory]
  [InlineData("v1.0.1.7", "v1.0.1")]
  [InlineData("v1.1.5.123", "v1.1.5")]
  [InlineData("1.2.3.4", "v1.2.3")]
  [InlineData("v1.2.3", "v1.2.3")]
  [InlineData("v0.0.0.0-dev", "v0.0.0-dev")]
  [InlineData("v1.2.3.4-beta.1", "v1.2.3-beta.1")]
  [InlineData("v1.2.3.4+abcdef", "v1.2.3")]
  [InlineData("v10.20.30.40", "v10.20.30")]
  [InlineData("", "v0.0.0-dev")]
  [InlineData(null, "v0.0.0-dev")]
  [InlineData("garbage", "v0.0.0-dev")]
  public void Drops_build_number_and_metadata(string? input, string expected)
  {
    Assert.Equal(expected, new VersionProvider(input).Version);
  }
}
