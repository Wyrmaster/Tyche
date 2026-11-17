using Tyche.Service.Requests;

namespace Tyche.Service.Images;

public interface IImageService
{
  /// <summary>Picks a random image of the requested collection and returns it, converted if requested.</summary>
  Task<ImageResult> GetRandomImageAsync(ImageRequest request, CancellationToken cancellationToken);
}
