namespace Tyche.Service.Configuration;

public sealed class ImageApiOptions
{
    public const string SectionName = "ImageApi";

    public Dictionary<string, string> Collections { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? DefaultCollection { get; set; }

    public int MaxDimension { get; set; } = 8192;

    public string CachePath { get; set; } = "/cache";

    public int CacheMaxSizeMb { get; set; } = 500;

    public string MagickPath { get; set; } = "magick";

    public int ProcessTimeoutSeconds { get; set; } = 30;

    public int MaxConcurrentProcesses { get; set; } = Environment.ProcessorCount;

    public int ListingCacheSeconds { get; set; } = 10;
}
