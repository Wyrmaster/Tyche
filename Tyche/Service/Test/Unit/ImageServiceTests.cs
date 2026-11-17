using Microsoft.Extensions.Logging.Abstractions;
using Tyche.Service.Caching;
using Tyche.Service.Catalog;
using Tyche.Service.Formats;
using Tyche.Service.Images;
using Tyche.Service.Processing;
using Tyche.Service.Requests;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class ImageServiceTests : IDisposable
{
  private readonly TempFolder _temp = new();

  public void Dispose() => _temp.Dispose();

  [Fact]
  public async Task Persistently_vanishing_files_give_unavailable_not_not_found()
  {
    var ghost = new CatalogImage(_temp.Combine("ghost.jpg"), ImageFormat.Jpg);
    var service = new ImageService(
      new StaleCatalog(ghost),
      new RandomImagePicker(),
      new ImageFormatMap(),
      new ImageProcessor(
        OptionsFactory.Create(),
        new MagickArgumentBuilder(OptionsFactory.Create(), new ImageFormatMap()),
        new FakeProcessRunner(),
        NullLogger<ImageProcessor>.Instance),
      new DiskImageCache(
        OptionsFactory.Create(options => options.CachePath = _temp.Sub("cache")),
        TimeProvider.System,
        NullLogger<DiskImageCache>.Instance));

    var result = await service.GetRandomImageAsync(
      new ImageRequest(null, null, null, 96, ResizeMode.Fit, null),
      CancellationToken.None);

    Assert.Equal(ImageResultStatus.Unavailable, result.Status);
  }

  private sealed class StaleCatalog : ICollectionCatalog
  {
    private readonly CatalogImage _image;

    public StaleCatalog(CatalogImage image) => _image = image;

    public CatalogResult GetImages(string? collectionName) => new(CatalogStatus.Found, "x", [_image]);

    public void Invalidate(string collectionName)
    {
    }
  }
}
