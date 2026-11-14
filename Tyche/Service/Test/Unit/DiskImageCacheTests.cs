using Microsoft.Extensions.Logging.Abstractions;
using Tyche.Service.Caching;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class DiskImageCacheTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly ManualTimeProvider time = new();

    public void Dispose() => temp.Dispose();

    private DiskImageCache Create(int maxMb = 500, string? folder = null) => new(
        OptionsFactory.Create(options =>
        {
            options.CachePath = folder ?? temp.Sub("cache");
            options.CacheMaxSizeMb = maxMb;
        }),
        time,
        NullLogger<DiskImageCache>.Instance);

    private static Func<string, CancellationToken, Task> Write(byte[] bytes, Action? onCall = null) => async (path, token) =>
    {
        onCall?.Invoke();
        await File.WriteAllBytesAsync(path, bytes, token);
    };

    private static async Task<byte[]> ReadAll(Stream stream)
    {
        await using (stream)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            return memory.ToArray();
        }
    }

    [Fact]
    public async Task Second_call_with_same_key_is_a_hit()
    {
        var cache = Create();
        var calls = 0;

        var first = await ReadAll(await cache.GetOrCreateAsync("k1", Write([1, 2, 3], () => calls++), default));
        var second = await ReadAll(await cache.GetOrCreateAsync("k1", Write([9], () => calls++), default));

        Assert.Equal(1, calls);
        Assert.Equal([1, 2, 3], first);
        Assert.Equal([1, 2, 3], second);
    }

    [Fact]
    public async Task Different_keys_are_separate_entries()
    {
        var cache = Create();

        var a = await ReadAll(await cache.GetOrCreateAsync("a", Write([1]), default));
        var b = await ReadAll(await cache.GetOrCreateAsync("b", Write([2]), default));

        Assert.Equal([1], a);
        Assert.Equal([2], b);
    }

    [Fact]
    public async Task Failed_creation_leaves_no_file_and_can_be_retried()
    {
        var folder = temp.Sub("cache");
        var cache = Create(folder: folder);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(
            "bad",
            async (path, token) =>
            {
                await File.WriteAllBytesAsync(path, [1, 2], token);
                throw new InvalidOperationException("conversion failed");
            },
            default));

        Assert.Empty(Directory.GetFiles(folder));

        var retried = await ReadAll(await cache.GetOrCreateAsync("bad", Write([7]), default));
        Assert.Equal([7], retried);
    }

    [Fact]
    public async Task Identical_simultaneous_requests_create_once()
    {
        var cache = Create();
        var calls = 0;
        var create = new Func<string, CancellationToken, Task>(async (path, token) =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(200, token);
            await File.WriteAllBytesAsync(path, [5, 5], token);
        });

        var results = await Task.WhenAll(Enumerable
            .Range(0, 10)
            .Select(async _ => await ReadAll(await cache.GetOrCreateAsync("same", create, default))));

        Assert.Equal(1, calls);
        Assert.All(results, bytes => Assert.Equal([5, 5], bytes));
    }

    [Fact]
    public async Task Different_keys_do_not_block_each_other()
    {
        var cache = Create();
        var gate = new TaskCompletionSource();
        var slow = cache.GetOrCreateAsync(
            "slow",
            async (path, token) =>
            {
                await gate.Task;
                await File.WriteAllBytesAsync(path, [1], token);
            },
            default);

        var fast = await ReadAll(await cache.GetOrCreateAsync("fast", Write([2]), default));

        Assert.Equal([2], fast);
        gate.SetResult();
        await ReadAll(await slow);
    }

    [Fact]
    public async Task Oldest_used_entries_are_evicted_above_the_limit()
    {
        var folder = temp.Sub("cache");
        var cache = Create(maxMb: 1, folder: folder);
        var big = new byte[400 * 1024];

        await ReadAll(await cache.GetOrCreateAsync("a", Write(big), default));
        time.Advance(TimeSpan.FromSeconds(1));
        await ReadAll(await cache.GetOrCreateAsync("b", Write(big), default));
        time.Advance(TimeSpan.FromSeconds(1));

        // Use "a" again so "b" becomes the least recently used.
        await ReadAll(await cache.GetOrCreateAsync("a", Write(big), default));
        time.Advance(TimeSpan.FromSeconds(1));
        await ReadAll(await cache.GetOrCreateAsync("c", Write(big), default));

        var remaining = Directory
            .GetFiles(folder)
            .Select(file => Path.GetFileName(file)!)
            .Order()
            .ToArray();
        Assert.Equal(["a", "c"], remaining);
    }

    [Fact]
    public async Task New_entry_larger_than_the_limit_is_still_served()
    {
        var cache = Create(maxMb: 1);

        var bytes = await ReadAll(await cache.GetOrCreateAsync("huge", Write(new byte[2 * 1024 * 1024]), default));

        Assert.Equal(2 * 1024 * 1024, bytes.Length);
    }

    [Fact]
    public async Task Eviction_under_parallel_load_never_fails_or_corrupts()
    {
        var folder = temp.Sub("cache");
        var cache = Create(maxMb: 1, folder: folder);
        var payload = new byte[100 * 1024];

        var tasks = Enumerable.Range(0, 200).Select(async index =>
        {
            var key = "k" + (index % 25);
            var expected = (byte)(index % 25);
            var bytes = await ReadAll(await cache.GetOrCreateAsync(
                key,
                async (path, token) => await File.WriteAllBytesAsync(
                    path,
                    payload
                        .Select(_ => expected)
                        .ToArray(),
                    token),
                default));
            Assert.Equal(payload.Length, bytes.Length);
            Assert.All(bytes.Take(10), value => Assert.Equal(expected, value));
        });
        await Task.WhenAll(tasks);

        Assert.True(Directory.GetFiles(folder).Sum(file => new FileInfo(file).Length) <= 1024 * 1024 + payload.Length);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    [Fact]
    public async Task Entry_larger_than_the_limit_survives_concurrent_writers()
    {
        var cache = Create(maxMb: 1);
        var payload = new byte[600 * 1024];

        var tasks = Enumerable.Range(0, 40).Select(async index =>
        {
            var bytes = await ReadAll(await cache.GetOrCreateAsync($"k{index}", Write(payload), default));
            Assert.Equal(payload.Length, bytes.Length);
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Failing_touch_still_returns_the_stream_on_hit_and_miss()
    {
        var folder = temp.Sub("cache");
        var cache = new FailingTouchCache(folder, time);

        var miss = await ReadAll(await cache.GetOrCreateAsync("k", Write([4, 2]), default));
        var hit = await ReadAll(await cache.GetOrCreateAsync("k", Write([9]), default));

        Assert.Equal([4, 2], miss);
        Assert.Equal([4, 2], hit);
    }

    private sealed class FailingTouchCache : DiskImageCache
    {
        public FailingTouchCache(string folder, TimeProvider time)
            : base(OptionsFactory.Create(options => options.CachePath = folder), time, NullLogger<DiskImageCache>.Instance)
        {
        }

        protected override void SetLastUse(string path) => throw new IOException("cannot touch");
    }

    [Fact]
    public void Leftover_temp_files_are_removed_at_start()
    {
        var folder = temp.Sub("cache");
        File.WriteAllBytes(Path.Combine(folder, "crashed.tmp"), [1]);
        File.WriteAllBytes(Path.Combine(folder, "keepme"), [1]);
        Directory.CreateDirectory(Path.Combine(folder, "tmp"));
        File.WriteAllBytes(Path.Combine(folder, "tmp", "magick-spill"), [1]);

        Create(folder: folder);

        Assert.False(File.Exists(Path.Combine(folder, "crashed.tmp")));
        Assert.True(File.Exists(Path.Combine(folder, "keepme")));
        Assert.False(File.Exists(Path.Combine(folder, "tmp", "magick-spill")));
        Assert.True(Directory.Exists(Path.Combine(folder, "tmp")));
    }

    [Fact]
    public void Cache_folder_is_created_when_missing()
    {
        var folder = temp.Combine("a", "b");

        Create(folder: folder);

        Assert.True(Directory.Exists(folder));
    }
}
