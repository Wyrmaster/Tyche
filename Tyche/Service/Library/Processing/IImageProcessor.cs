using Tyche.Service.Catalog;
using Tyche.Service.Requests;

namespace Tyche.Service.Processing;

public interface IImageProcessor
{
    /// <summary>Converts the source to <paramref name="outputPath"/>. Throws <see cref="ImageProcessingException"/> on failure.</summary>
    Task ProcessAsync(CatalogImage source, ImageRequest request, string outputPath, CancellationToken cancellationToken);
}
