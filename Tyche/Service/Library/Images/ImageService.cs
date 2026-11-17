using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tyche.Service.Caching;
using Tyche.Service.Catalog;
using Tyche.Service.Formats;
using Tyche.Service.Processing;
using Tyche.Service.Requests;

namespace Tyche.Service.Images;

public sealed class ImageService : IImageService
{
  private const int MaxAttempts = 3;

  private readonly ICollectionCatalog catalog;
  private readonly IRandomImagePicker picker;
  private readonly IImageFormatMap formats;
  private readonly IImageProcessor processor;
  private readonly IImageCache cache;

  public ImageService(
    ICollectionCatalog catalog,
    IRandomImagePicker picker,
    IImageFormatMap formats,
    IImageProcessor processor,
    IImageCache cache)
  {
    this.catalog = catalog;
    this.picker = picker;
    this.formats = formats;
    this.processor = processor;
    this.cache = cache;
  }

  public async Task<ImageResult> GetRandomImageAsync(ImageRequest request, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < MaxAttempts; attempt++)
    {
      var listing = catalog.GetImages(request.Collection);
      if (listing.Status == CatalogStatus.UnknownCollection)
      {
        return ImageResult.NotFound($"Collection '{request.Collection}' does not exist.");
      }

      if (listing.Status == CatalogStatus.Empty)
      {
        return ImageResult.NotFound($"Collection '{listing.CollectionName}' contains no images.");
      }

      var source = picker.Pick(listing.Images);
      FileInfo file = new(source.Path);
      if (!file.Exists)
      {
        // The listing is stale (file removed within the listing cache time): rescan and pick again.
        catalog.Invalidate(listing.CollectionName);
        continue;
      }

      if (request.IsPassThrough)
      {
        try
        {
          return ImageResult.Ok(OpenOriginal(source.Path), formats.GetContentType(source.Format));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
          catalog.Invalidate(listing.CollectionName);
          continue;
        }
      }

      var target = request.Format ?? source.Format;
      var stream = await cache.GetOrCreateAsync(
        CacheKey(source, file, request, target),
        (outputPath, token) => processor.ProcessAsync(source, request, outputPath, token),
        cancellationToken);
      return ImageResult.Ok(stream, formats.GetContentType(target));
    }

    return ImageResult.Unavailable("No image could be read from the collection right now, try again.");
  }

  private static FileStream OpenOriginal(string path) => new(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.ReadWrite | FileShare.Delete,
    bufferSize: 4096,
    FileOptions.Asynchronous | FileOptions.SequentialScan);

  private static string CacheKey(CatalogImage source, FileInfo file, ImageRequest request, ImageFormat target)
  {
    var text = string.Join(
      '\n',
      "v1",
      source.Path,
      file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
      file.Length.ToString(CultureInfo.InvariantCulture),
      request.Width?.ToString(CultureInfo.InvariantCulture) ?? "-",
      request.Height?.ToString(CultureInfo.InvariantCulture) ?? "-",
      request.Mode,
      target);
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
    return Convert
      .ToHexString(hash)
      .ToLowerInvariant();
  }
}
