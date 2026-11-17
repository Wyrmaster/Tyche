using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyche.Service.Configuration;

namespace Tyche.Service.Caching;

public class DiskImageCache : IImageCache
{
  private const string TempExtension = ".tmp";

  private readonly string folder;
  private readonly long maxBytes;
  private readonly TimeProvider time;
  private readonly ILogger<DiskImageCache> logger;
  private readonly Dictionary<string, KeyLock> locks = new();
  private readonly SemaphoreSlim eviction = new(1, 1);

  public DiskImageCache(IOptions<ImageApiOptions> options, TimeProvider time, ILogger<DiskImageCache> logger)
  {
    folder = Path.GetFullPath(options.Value.CachePath);
    maxBytes = options.Value.CacheMaxSizeMb * 1024L * 1024L;
    this.time = time;
    this.logger = logger;

    Directory.CreateDirectory(folder);
    Directory.CreateDirectory(MagickTemporaryFolder(folder));
    DeleteTempFiles();
  }

  /// <summary>Folder for ImageMagick's own spill files, inside the cache folder (outside the eviction scan).</summary>
  public static string MagickTemporaryFolder(string cacheFolder) => Path.Combine(Path.GetFullPath(cacheFolder), "tmp");

  public async Task<Stream> GetOrCreateAsync(
    string key,
    Func<string, CancellationToken, Task> create,
    CancellationToken cancellationToken)
  {
    var path = PathFor(key);
    var hit = TryOpen(path);
    if (hit is not null)
    {
      return hit;
    }

    var keyLock = Acquire(key);
    await keyLock.Semaphore.WaitAsync(cancellationToken);
    try
    {
      hit = TryOpen(path);
      if (hit is not null)
      {
        return hit;
      }

      var temp = Path.Combine(folder, $"{Guid.NewGuid():N}{TempExtension}");
      try
      {
        await create(temp, cancellationToken);

        // Move, touch, open and evict under one guard: a concurrent eviction cannot delete the
        // just-written file before it is open, and a new entry larger than the limit is still served.
        await eviction.WaitAsync(cancellationToken);
        try
        {
          File.Move(temp, path, overwrite: true);
          TouchSafely(path);
          var stream = Open(path);
          Evict(path);
          return stream;
        }
        finally
        {
          eviction.Release();
        }
      }
      finally
      {
        if (File.Exists(temp))
        {
          File.Delete(temp);
        }
      }
    }
    finally
    {
      keyLock.Semaphore.Release();
      Release(key, keyLock);
    }
  }

  private string PathFor(string key) => Path.Combine(folder, key);

  /// <summary>Marks the entry as just used (LRU). A failure here must never fail a request.</summary>
  protected virtual void SetLastUse(string path)
  {
    File.SetLastWriteTimeUtc(path, time.GetUtcNow().UtcDateTime);
  }

  private FileStream? TryOpen(string path)
  {
    FileStream stream;
    try
    {
      stream = Open(path);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Missing or evicted: treat as a miss.
      return null;
    }

    TouchSafely(path);
    return stream;
  }

  private void TouchSafely(string path)
  {
    try
    {
      SetLastUse(path);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      logger.LogWarning(exception, "Could not update the last use of cache file {File}", Path.GetFileName(path));
    }
  }

  private static FileStream Open(string path) => new(
    path,
    FileMode.Open,
    FileAccess.Read,
    FileShare.ReadWrite | FileShare.Delete,
    bufferSize: 4096,
    FileOptions.Asynchronous | FileOptions.SequentialScan);

  private void Evict(string keep)
  {
    var files = new DirectoryInfo(folder)
      .EnumerateFiles()
      .Where(file => file.Extension != TempExtension)
      .ToList();

    var total = files.Sum(file => file.Length);
    if (total <= maxBytes)
    {
      return;
    }

    foreach (var file in files.Where(file => file.FullName != keep).OrderBy(file => file.LastWriteTimeUtc))
    {
      if (total <= maxBytes)
      {
        break;
      }

      try
      {
        var length = file.Length;
        file.Delete();
        total -= length;
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        logger.LogWarning(exception, "Could not evict cache file {File}", file.Name);
      }
    }
  }

  private void DeleteTempFiles()
  {
    foreach (var file in Directory.EnumerateFiles(MagickTemporaryFolder(folder)))
    {
      try
      {
        File.Delete(file);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        logger.LogWarning(exception, "Could not delete leftover ImageMagick file {File}", file);
      }
    }

    foreach (var file in Directory.EnumerateFiles(folder, $"*{TempExtension}"))
    {
      try
      {
        File.Delete(file);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        logger.LogWarning(exception, "Could not delete leftover temp file {File}", file);
      }
    }
  }

  private KeyLock Acquire(string key)
  {
    lock (locks)
    {
      if (!locks.TryGetValue(key, out var keyLock))
      {
        keyLock = new KeyLock();
        locks[key] = keyLock;
      }

      keyLock.Users++;
      return keyLock;
    }
  }

  private void Release(string key, KeyLock keyLock)
  {
    lock (locks)
    {
      if (--keyLock.Users == 0)
      {
        locks.Remove(key);
      }
    }
  }

  private sealed class KeyLock
  {
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public int Users { get; set; }
  }
}
