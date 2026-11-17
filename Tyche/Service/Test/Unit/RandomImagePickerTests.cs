using Tyche.Service.Catalog;
using Tyche.Service.Formats;

namespace Tyche.Service.Test.Unit;

public class RandomImagePickerTests
{
  [Fact]
  public void Pick_is_roughly_uniform_and_covers_all_files()
  {
    var images = Enumerable
      .Range(0, 5)
      .Select(index => new CatalogImage($"/img/{index}.jpg", ImageFormat.Jpg))
      .ToList();
    var picker = new RandomImagePicker();

    var counts = Enumerable
      .Range(0, 5000)
      .Select(_ => picker.Pick(images))
      .GroupBy(image => image.Path)
      .ToDictionary(group => group.Key, group => group.Count());

    Assert.Equal(5, counts.Count);
    Assert.All(counts.Values, count => Assert.InRange(count, 800, 1200));
  }

  [Fact]
  public void Single_image_is_always_picked()
  {
    var image = new CatalogImage("/img/a.png", ImageFormat.Png);

    Assert.Same(image, new RandomImagePicker().Pick([image]));
  }
}
