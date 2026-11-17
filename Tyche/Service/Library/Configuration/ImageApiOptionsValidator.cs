using Microsoft.Extensions.Options;

namespace Tyche.Service.Configuration;

public sealed class ImageApiOptionsValidator : IValidateOptions<ImageApiOptions>
{
  public ValidateOptionsResult Validate(string? name, ImageApiOptions options)
  {
    var failures = new List<string>();
    var section = ImageApiOptions.SectionName;

    if (options.Collections.Count == 0)
    {
      failures.Add($"{section}:Collections is empty. Configure at least one collection (name -> folder), "
        + $"for example {section}__Collections__nature=/images/nature.");
    }

    foreach (var (collectionName, folder) in options.Collections)
    {
      if (string.IsNullOrWhiteSpace(folder))
      {
        failures.Add($"{section}:Collections:{collectionName} has no folder path.");
      }
      else if (!Directory.Exists(folder))
      {
        failures.Add($"{section}:Collections:{collectionName}: folder '{folder}' does not exist.");
      }
      else if (!IsReadable(folder))
      {
        failures.Add($"{section}:Collections:{collectionName}: folder '{folder}' is not readable.");
      }
    }

    if (string.IsNullOrWhiteSpace(options.DefaultCollection))
    {
      failures.Add($"{section}:DefaultCollection is not set.");
    }
    else if (!options.Collections.ContainsKey(options.DefaultCollection))
    {
      failures.Add($"{section}:DefaultCollection '{options.DefaultCollection}' is not one of the configured "
        + $"collections ({string.Join(", ", options.Collections.Keys)}).");
    }

    RequirePositive(failures, nameof(options.MaxDimension), options.MaxDimension);
    RequirePositive(failures, nameof(options.CacheMaxSizeMb), options.CacheMaxSizeMb);
    RequirePositive(failures, nameof(options.ProcessTimeoutSeconds), options.ProcessTimeoutSeconds);
    RequirePositive(failures, nameof(options.MaxConcurrentProcesses), options.MaxConcurrentProcesses);
    RequirePositive(failures, nameof(options.ListingCacheSeconds), options.ListingCacheSeconds);

    if (string.IsNullOrWhiteSpace(options.MagickPath))
    {
      failures.Add($"{section}:MagickPath is empty.");
    }

    if (string.IsNullOrWhiteSpace(options.CachePath))
    {
      failures.Add($"{section}:CachePath is empty.");
    }
    else if (options.CachePath.Contains('%'))
    {
      // ImageMagick expands %-patterns in output file names.
      failures.Add($"{section}:CachePath '{options.CachePath}' must not contain '%'.");
    }
    else if (!IsWritable(options.CachePath, out var reason))
    {
      failures.Add($"{section}:CachePath '{options.CachePath}' is not usable: {reason}");
    }

    return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
  }

  private static void RequirePositive(List<string> failures, string key, int value)
  {
    if (value <= 0)
    {
      failures.Add($"{ImageApiOptions.SectionName}:{key} must be greater than 0 (is {value}).");
    }
  }

  private static bool IsReadable(string folder)
  {
    try
    {
      using var entries = Directory.EnumerateFileSystemEntries(folder).GetEnumerator();
      entries.MoveNext();
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return false;
    }
  }

  private static bool IsWritable(string folder, out string reason)
  {
    try
    {
      Directory.CreateDirectory(folder);
      var probe = Path.Combine(folder, $".probe-{Guid.NewGuid():N}.tmp");
      File.WriteAllBytes(probe, []);
      File.Delete(probe);
      reason = string.Empty;
      return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      reason = exception.Message;
      return false;
    }
  }
}
