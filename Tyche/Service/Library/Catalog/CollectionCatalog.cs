using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Tyche.Service.Configuration;
using Tyche.Service.Formats;

namespace Tyche.Service.Catalog;

public sealed class CollectionCatalog : ICollectionCatalog
{
  private static readonly EnumerationOptions Enumeration = new()
  {
    RecurseSubdirectories = true,
    IgnoreInaccessible = true,
    MatchType = MatchType.Simple,
    ReturnSpecialDirectories = false,
  };

  private readonly ImageApiOptions options;
  private readonly IImageFormatMap formats;
  private readonly TimeProvider time;
  private readonly ConcurrentDictionary<string, Listing> listings = new(StringComparer.OrdinalIgnoreCase);
  private readonly ConcurrentDictionary<string, object> gates = new(StringComparer.OrdinalIgnoreCase);

  public CollectionCatalog(IOptions<ImageApiOptions> options, IImageFormatMap formats, TimeProvider time)
  {
    this.options = options.Value;
    this.formats = formats;
    this.time = time;
  }

  public CatalogResult GetImages(string? collectionName)
  {
    var name = string.IsNullOrEmpty(collectionName) ? options.DefaultCollection : collectionName;
    if (name is null || !options.Collections.TryGetValue(name, out var folder))
    {
      return new CatalogResult(CatalogStatus.UnknownCollection, name ?? string.Empty, []);
    }

    var listing = GetListing(name, folder);
    var status = listing.Images.Count == 0 ? CatalogStatus.Empty : CatalogStatus.Found;
    return new CatalogResult(status, name, listing.Images);
  }

  // One rescan per collection at a time: while one request rescans an expired listing, others keep using the
  // stale one; only when there is no listing at all do they wait for the rescan.
  private Listing GetListing(string name, string folder)
  {
    var ttl = TimeSpan.FromSeconds(options.ListingCacheSeconds);
    if (listings.TryGetValue(name, out var listing) && time.GetUtcNow() - listing.ScannedAt < ttl)
    {
      return listing;
    }

    var gate = gates.GetOrAdd(name, _ => new object());
    var taken = false;
    try
    {
      if (listing is null)
      {
        Monitor.Enter(gate, ref taken);
      }
      else
      {
        Monitor.TryEnter(gate, ref taken);
        if (!taken)
        {
          return listing;
        }
      }

      if (listings.TryGetValue(name, out var current) && time.GetUtcNow() - current.ScannedAt < ttl)
      {
        return current;
      }

      var scanned = new Listing(time.GetUtcNow(), Scan(folder));
      listings[name] = scanned;
      return scanned;
    }
    finally
    {
      if (taken)
      {
        Monitor.Exit(gate);
      }
    }
  }

  public void Invalidate(string collectionName) => listings.TryRemove(collectionName, out _);

  private List<CatalogImage> Scan(string folder)
  {
    var images = new List<CatalogImage>();
    try
    {
      foreach (var file in Directory.EnumerateFiles(Path.GetFullPath(folder), "*", Enumeration))
      {
        if (formats.TryFromExtension(Path.GetExtension(file), out var format))
        {
          images.Add(new CatalogImage(file, format));
        }
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // The folder disappeared after startup: report it as an empty collection.
    }

    return images;
  }

  private sealed record Listing(DateTimeOffset ScannedAt, IReadOnlyList<CatalogImage> Images);
}
