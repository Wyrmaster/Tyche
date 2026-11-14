using System.Text.RegularExpressions;

namespace Tyche.Service.Versioning;

public sealed partial class VersionProvider : IVersionProvider
{
    public VersionProvider(string? informationalVersion)
    {
        Version = Format(informationalVersion);
    }

    public string Version { get; }

    // v1.0.1.7 -> v1.0.1, v0.0.0.0-dev -> v0.0.0-dev, 1.2.3+abc -> v1.2.3
    private static string Format(string? informationalVersion)
    {
        var match = VersionPattern().Match(informationalVersion ?? string.Empty);
        return match.Success
            ? $"v{match.Groups["major"].Value}.{match.Groups["minor"].Value}.{match.Groups["patch"].Value}{match.Groups["suffix"].Value}"
            : "v0.0.0-dev";
    }

    [GeneratedRegex(@"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?(?<suffix>-[0-9A-Za-z.-]+)?(?:\+.*)?$")]
    private static partial Regex VersionPattern();
}
