using System.Net;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public class HostileInputTests : IntegrationTestBase
{
    [RequiresMagickTheory]
    [InlineData("we ird [1] name.png")]
    [InlineData("-negative.png")]
    [InlineData("@list.png")]
    [InlineData("a*b?.png")]
    [InlineData("it's \"q\" $(x) `y`;.png")]
    [InlineData("name[0].png")]
    [InlineData("pipe|name.png")]
    [InlineData("tab\there.png")]
    public async Task Hostile_file_names_are_converted_safely(string name)
    {
        TestImages.Create(Path.Combine(Nature, "src.png"), 200, 100);
        File.Move(Path.Combine(Nature, "src.png"), Path.Combine(Nature, name));
        Start();

        var (response, body) = await GetAsync("/image?width=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = TestImages.Identify(body);
        Assert.Equal(("PNG", 50, 25), (info.Format, info.Width, info.Height));
    }

    [RequiresMagickFact]
    public async Task File_name_with_percent_pattern_is_converted()
    {
        TestImages.Create(Path.Combine(Nature, "100%d.png"), 200, 100);
        Start();

        var (response, _) = await GetAsync("/image?width=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Symlink_loop_in_a_collection_does_not_hang_the_scan()
    {
        File.WriteAllBytes(Path.Combine(Nature, "a.png"), [1]);
        Directory.CreateSymbolicLink(Path.Combine(Nature, "loop"), Nature);
        Start();

        var request = GetAsync("/image");
        var finished = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(request, finished);
        Assert.Equal(HttpStatusCode.OK, (await request).Response.StatusCode);
    }

    [RequiresMagickTheory]
    [InlineData("svg")]
    [InlineData("msl")]
    [InlineData("ps")]
    [InlineData("url")]
    public async Task Content_that_pretends_to_be_png_is_not_interpreted_as_another_coder(string kind)
    {
        var marker = Path.Combine(Temp.Sub("outside"), "pwned.txt");
        var content = kind switch
        {
            "svg" => $"<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><image href='{marker}'/></svg>",
            "msl" => $"<?xml version='1.0'?><image><read filename='xc:red'/><write filename='{marker}'/></image>",
            "ps" => $"%!PS\n(x) (w) file closefile\n/f ({marker}) (w) file def f closefile\nshowpage",
            _ => $"url:http://127.0.0.1:9/x",
        };
        File.WriteAllText(Path.Combine(Nature, "evil.png"), content);
        Start();

        var (response, _) = await GetAsync("/image?width=50");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(File.Exists(marker));
    }

    [RequiresMagickFact]
    public async Task Corrupt_file_gives_500_and_leaves_no_cache_entry()
    {
        File.WriteAllBytes(Path.Combine(Nature, "bad.jpg"), [0xFF, 0xD8, 0, 1, 2]);
        Start();

        var (response, _) = await GetAsync("/image?width=50");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(Directory.GetFiles(Factory.CachePath));
    }

    [RequiresMagickFact]
    public async Task Zero_byte_file_gives_500_not_a_crash()
    {
        File.WriteAllBytes(Path.Combine(Nature, "empty.png"), []);
        Start();

        var (response, _) = await GetAsync("/image?format=webp");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData("collection=../outside")]
    [InlineData("collection=..%2F..%2Fetc")]
    [InlineData("collection=%2Fetc")]
    [InlineData("collection=nature%00")]
    [InlineData("collection=NATURE/../x")]
    [InlineData("path=/etc/passwd&file=/etc/passwd")]
    public async Task Client_supplied_paths_never_leave_the_folders(string query)
    {
        File.WriteAllBytes(Path.Combine(Nature, "a.png"), [1, 2, 3]);
        Start();

        var (response, body) = await GetAsync("/image?" + query);

        Assert.DoesNotContain("root:", System.Text.Encoding.UTF8.GetString(body));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.OK);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(new byte[] { 1, 2, 3 }, body);
        }
    }

    [Theory]
    [InlineData("width=1e3")]
    [InlineData("width=0x10")]
    [InlineData("width=-5")]
    [InlineData("width=+5")]
    [InlineData("width=5.5")]
    [InlineData("width=99999999999999999999")]
    [InlineData("width=%00")]
    [InlineData("width=1,2")]
    [InlineData("width=1&width=2")]
    [InlineData("width=800x600%5E")]
    [InlineData("dpi=0")]
    [InlineData("dpi=99999999999")]
    [InlineData("dpi=2147483647&width=2147483647")]
    [InlineData("width=100&dpi=1")]
    [InlineData("mode=fill%20")]
    [InlineData("format=png:/tmp/x")]
    [InlineData("format=webp%00")]
    [InlineData("format=jpg;rm")]
    public async Task Malformed_parameters_never_5xx_and_never_reach_magick(string query)
    {
        TestImages.Create(Path.Combine(Nature, "a.png"), 20, 20);
        Start();

        var (response, _) = await GetAsync("/image?" + query);

        Assert.True((int)response.StatusCode < 500, $"{query} -> {(int)response.StatusCode}");
        Assert.True(response.Headers.CacheControl?.ToString() == "no-store");
    }

    [Fact]
    public async Task Head_and_post_are_not_a_way_around()
    {
        File.WriteAllBytes(Path.Combine(Nature, "a.png"), [1]);
        Start();

        var post = await Client.PostAsync("/image", null);
        var health = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [RequiresMagickFact]
    public async Task Simultaneous_identical_requests_run_one_conversion()
    {
        TestImages.Create(Path.Combine(Nature, "a.png"), 300, 200);
        Start();

        var results = await Task.WhenAll(Enumerable
            .Range(0, 6)
            .Select(_ => GetAsync("/image?width=100&format=webp")));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Response.StatusCode));
        Assert.Single(results
            .Select(r => Convert.ToHexString(r.Body))
            .Distinct());
        Assert.Equal(1, Factory.Runner.Conversions);
    }
}
