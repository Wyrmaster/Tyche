namespace Tyche.Service.Catalog;

public interface ICollectionCatalog
{
    /// <summary>Lists the images of a collection; null or empty name means the default collection.</summary>
    CatalogResult GetImages(string? collectionName);

    /// <summary>Drops the cached listing, for example after a file vanished.</summary>
    void Invalidate(string collectionName);
}
