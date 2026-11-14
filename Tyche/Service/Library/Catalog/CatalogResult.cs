namespace Tyche.Service.Catalog;

public sealed record CatalogResult(CatalogStatus Status, string CollectionName, IReadOnlyList<CatalogImage> Images);
