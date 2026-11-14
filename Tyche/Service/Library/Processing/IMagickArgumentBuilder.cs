using Tyche.Service.Catalog;
using Tyche.Service.Requests;

namespace Tyche.Service.Processing;

public interface IMagickArgumentBuilder
{
    /// <summary>Builds the argument list (no shell, one element per argument) for converting a source image.</summary>
    IReadOnlyList<string> Build(CatalogImage source, ImageRequest request, string outputPath);
}
