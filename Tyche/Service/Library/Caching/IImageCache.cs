namespace Tyche.Service.Caching;

public interface IImageCache
{
  /// <summary>
  /// Returns the cached file for the key as a readable stream. On a miss, <paramref name="create"/> is called
  /// with a temporary file path to fill; identical keys run one creation at a time.
  /// </summary>
  Task<Stream> GetOrCreateAsync(
    string key,
    Func<string, CancellationToken, Task> create,
    CancellationToken cancellationToken);
}
