using Tyche.Service.Configuration;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class ImageApiOptionsValidatorTests
{
  private readonly ImageApiOptionsValidator _validator = new();

  private static ImageApiOptions Valid(TempFolder temp) => new()
  {
    Collections = { ["nature"] = temp.Sub("nature") },
    DefaultCollection = "nature",
    CachePath = temp.Sub("cache"),
  };

  [Fact]
  public void Defaults_match_the_plan()
  {
    var options = new ImageApiOptions();
    Assert.Equal(8192, options.MaxDimension);
    Assert.Equal("/cache", options.CachePath);
    Assert.Equal(500, options.CacheMaxSizeMb);
    Assert.Equal("magick", options.MagickPath);
    Assert.Equal(30, options.ProcessTimeoutSeconds);
    Assert.Equal(Environment.ProcessorCount, options.MaxConcurrentProcesses);
    Assert.Equal(10, options.ListingCacheSeconds);
  }

  [Fact]
  public void Valid_options_pass()
  {
    using var temp = new TempFolder();
    Assert.True(_validator.Validate(null, Valid(temp)).Succeeded);
  }

  [Fact]
  public void No_collections_is_reported()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.Collections.Clear();

    var result = _validator.Validate(null, options);

    Assert.True(result.Failed);
    Assert.Contains(result.Failures!, failure => failure.Contains("Collections is empty"));
  }

  [Fact]
  public void Missing_default_is_reported()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.DefaultCollection = "other";

    var result = _validator.Validate(null, options);

    Assert.Contains(result.Failures!, failure => failure.Contains("DefaultCollection 'other'"));
  }

  [Fact]
  public void Unset_default_is_reported()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.DefaultCollection = null;

    Assert.Contains(_validator.Validate(null, options).Failures!, failure => failure.Contains("not set"));
  }

  [Fact]
  public void Missing_folder_is_reported_with_its_path()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.Collections["gone"] = temp.Combine("does-not-exist");

    var result = _validator.Validate(null, options);

    Assert.Contains(result.Failures!, failure => failure.Contains("does-not-exist") && failure.Contains("gone"));
  }

  [Fact]
  public void Non_positive_limits_are_reported()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.MaxDimension = 0;
    options.CacheMaxSizeMb = -1;
    options.ProcessTimeoutSeconds = 0;
    options.MaxConcurrentProcesses = 0;
    options.ListingCacheSeconds = 0;

    Assert.Equal(5, _validator.Validate(null, options).Failures!.Count());
  }

  [Fact]
  public void Percent_in_cache_path_is_rejected()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.CachePath = temp.Combine("cache%d");

    Assert.Contains(_validator.Validate(null, options).Failures!, failure => failure.Contains("'%'"));
  }

  [Fact]
  public void Cache_path_is_created_when_missing()
  {
    using var temp = new TempFolder();
    var options = Valid(temp);
    options.CachePath = temp.Combine("new", "cache");

    Assert.True(_validator.Validate(null, options).Succeeded);
    Assert.True(Directory.Exists(options.CachePath));
  }
}
