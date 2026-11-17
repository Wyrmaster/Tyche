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

  private readonly ICollectionCatalog _catalog;
  private readonly IRandomImagePicker _picker;
  private readonly IImageFormatMap _formats;
  private readonly IImageProcessor _processor;
  private readonly IImageCache _cache;

  public ImageService(
    ICollectionCatalog catalog,
    IRandomImagePicker picker,
    IImageFormatMap formats,
    IImageProcessor processor,
    IImageCache cache)
  {
    _catalog = catalog;
    _picker = picker;
    _formats = formats;
    _processor = processor;
    _cache = cache;
  }

  public async Task<ImageResult> GetRandomImageAsync(ImageRequest request, CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < MaxAttempts; attempt++)
    {
      var result = await TryGetRandomImageAsync(request, cancellationToken);
      if (result is not null)
      {
        return result;
      }
    }

    return ImageResult.Unavailable("No image could be read from the collection right now, try again.");
  }

  // Returns null when the picked file vanished and the caller should try again.
  private async Task<ImageResult?> TryGetRandomImageAsync(ImageRequest request, CancellationToken cancellationToken)
  {
    var listing = _catalog.GetImages(request.Collection);
    if (listing.Status == CatalogStatus.UnknownCollection)
    {
      return ImageResult.NotFound($"Collection '{request.Collection}' does not exist.");
    }

    if (listing.Status == CatalogStatus.Empty)
    {
      return ImageResult.NotFound($"Collection '{listing.CollectionName}' contains no images.");
    }

    var source = _picker.Pick(listing.Images);
    FileInfo file = new(source.Path);
    if (!file.Exists)
    {
      // The listing is stale (file removed within the listing cache time): rescan and pick again.
      _catalog.Invalidate(listing.CollectionName);
      return null;
    }

    if (request.IsPassThrough)
    {
      try
      {
        return ImageResult.Ok(OpenOriginal(source.Path), _formats.GetContentType(source.Format));
      }
      catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
      {
        _catalog.Invalidate(listing.CollectionName);
        return null;
      }
    }

    var target = request.Format ?? source.Format;
    var stream = await _cache.GetOrCreateAsync(
      CacheKey(source, file, request, target),
      (outputPath, token) => _processor.ProcessAsync(source, request, outputPath, token),
      cancellationToken);
    return ImageResult.Ok(stream, _formats.GetContentType(target));
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
