namespace Tyche.Service.Catalog;

public interface IRandomImagePicker
{
    /// <summary>Picks one image uniformly at random; the list must not be empty.</summary>
    CatalogImage Pick(IReadOnlyList<CatalogImage> images);
}
