using Tyche.Service.Catalog;
using Tyche.Service.Formats;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class CollectionCatalogTests : IDisposable
{
    private readonly TempFolder temp = new();
    private readonly ManualTimeProvider time = new();

    public void Dispose() => temp.Dispose();

    private CollectionCatalog Create(params (string Name, string Folder)[] collections) => new(
        OptionsFactory.Create(options =>
        {
            foreach (var (name, folder) in collections)
            {
                options.Collections[name] = folder;
            }

            options.DefaultCollection = collections[0].Name;
            options.ListingCacheSeconds = 10;
        }),
        new ImageFormatMap(),
        time);

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
    }

    [Fact]
    public void Scans_recursively_and_ignores_other_types_case_insensitive()
    {
        var folder = temp.Sub("nature");
        Touch(Path.Combine(folder, "a.jpg"));
        Touch(Path.Combine(folder, "b.JPEG"));
        Touch(Path.Combine(folder, "deep", "er", "c.PNG"));
        Touch(Path.Combine(folder, "d.webp"));
        Touch(Path.Combine(folder, "e.gif"));
        Touch(Path.Combine(folder, "notes.txt"));
        Touch(Path.Combine(folder, "f.bmp"));
        Touch(Path.Combine(folder, "noextension"));

        var result = Create(("nature", folder)).GetImages("nature");

        Assert.Equal(CatalogStatus.Found, result.Status);
        Assert.Equal(
            ["a.jpg", "b.JPEG", "c.PNG", "d.webp", "e.gif"],
            result.Images
                .Select(image => Path.GetFileName(image.Path))
                .Order()
                .ToArray());
        Assert.Equal(ImageFormat.Jpg, result.Images.Single(image => image.Path.EndsWith("b.JPEG")).Format);
    }

    [Fact]
    public void Named_collection_is_served_from_its_folder_and_default_used_without_name()
    {
        var nature = temp.Sub("nature");
        var city = temp.Sub("city");
        Touch(Path.Combine(nature, "n.jpg"));
        Touch(Path.Combine(city, "c.jpg"));
        var catalog = Create(("nature", nature), ("city", city));

        Assert.EndsWith("c.jpg", catalog.GetImages("city").Images.Single().Path);
        Assert.EndsWith("n.jpg", catalog.GetImages(null).Images.Single().Path);
        Assert.EndsWith("n.jpg", catalog.GetImages("").Images.Single().Path);
        Assert.EndsWith("c.jpg", catalog.GetImages("CITY").Images.Single().Path);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("../nature")]
    [InlineData("/etc")]
    [InlineData("nature/../../etc")]
    public void Unknown_names_and_path_like_names_are_not_found(string name)
    {
        var folder = temp.Sub("nature");
        Touch(Path.Combine(folder, "n.jpg"));

        var result = Create(("nature", folder)).GetImages(name);

        Assert.Equal(CatalogStatus.UnknownCollection, result.Status);
        Assert.Empty(result.Images);
    }

    [Fact]
    public void Collection_without_images_is_empty()
    {
        var folder = temp.Sub("empty");
        Touch(Path.Combine(folder, "readme.txt"));

        Assert.Equal(CatalogStatus.Empty, Create(("empty", folder)).GetImages("empty").Status);
    }

    [Fact]
    public void Listing_is_cached_until_ttl_then_picks_up_changes()
    {
        var folder = temp.Sub("nature");
        Touch(Path.Combine(folder, "a.jpg"));
        var catalog = Create(("nature", folder));
        Assert.Single(catalog.GetImages("nature").Images);

        Touch(Path.Combine(folder, "b.jpg"));
        Assert.Single(catalog.GetImages("nature").Images);

        time.Advance(TimeSpan.FromSeconds(11));
        Assert.Equal(2, catalog.GetImages("nature").Images.Count);

        File.Delete(Path.Combine(folder, "a.jpg"));
        time.Advance(TimeSpan.FromSeconds(11));
        Assert.Single(catalog.GetImages("nature").Images);
    }

    [Fact]
    public void Invalidate_forces_a_rescan()
    {
        var folder = temp.Sub("nature");
        Touch(Path.Combine(folder, "a.jpg"));
        var catalog = Create(("nature", folder));
        Assert.Single(catalog.GetImages("nature").Images);

        Touch(Path.Combine(folder, "b.jpg"));
        catalog.Invalidate("nature");

        Assert.Equal(2, catalog.GetImages("nature").Images.Count);
    }

    [Fact]
    public void Parallel_requests_at_expiry_all_get_a_listing()
    {
        var folder = temp.Sub("nature");
        for (var i = 0; i < 200; i++)
        {
            Touch(Path.Combine(folder, $"{i}.jpg"));
        }

        var catalog = Create(("nature", folder));
        catalog.GetImages("nature");

        for (var round = 0; round < 3; round++)
        {
            time.Advance(TimeSpan.FromSeconds(11));
            var counts = Enumerable
                .Range(0, 32)
                .AsParallel()
                .WithDegreeOfParallelism(16)
                .Select(_ => catalog.GetImages("nature").Images.Count)
                .ToArray();

            Assert.All(counts, count => Assert.Equal(200, count));
        }
    }

    [Fact]
    public void Folder_that_disappears_is_reported_as_empty_not_thrown()
    {
        var folder = temp.Sub("nature");
        var catalog = Create(("nature", folder));
        Directory.Delete(folder);

        Assert.Equal(CatalogStatus.Empty, catalog.GetImages("nature").Status);
    }
}
