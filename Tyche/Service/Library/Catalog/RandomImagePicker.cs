namespace Tyche.Service.Catalog;

public sealed class RandomImagePicker : IRandomImagePicker
{
    public CatalogImage Pick(IReadOnlyList<CatalogImage> images) => images[Random.Shared.Next(images.Count)];
}
