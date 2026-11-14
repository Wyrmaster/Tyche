using System.Net;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Integration;

public class VersionAndHealthTests : IntegrationTestBase
{
    [Fact]
    public async Task Version_returns_three_part_version_of_the_build()
    {
        Start();

        var (response, body) = await GetAsync("/version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = System.Text.Encoding.UTF8.GetString(body);
        Assert.Matches(@"^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", text);
        Assert.Equal("v0.0.0-dev", text); // local build without tag
    }

    [Fact]
    public async Task Informational_version_of_the_assembly_is_the_dev_default()
    {
        var attribute = (System.Reflection.AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
            typeof(Program).Assembly,
            typeof(System.Reflection.AssemblyInformationalVersionAttribute))!;

        Assert.Equal("v0.0.0.0-dev", attribute.InformationalVersion);
        Assert.Equal("0.0.0.0", typeof(Program).Assembly.GetName().Version!.ToString());
        await Task.CompletedTask;
    }

    [RequiresMagickFact]
    public async Task Health_is_200_when_magick_works()
    {
        Start();

        var (response, _) = await GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_503_when_magick_is_not_usable()
    {
        Prepare(("nature", Nature)).With("MagickPath", "/no/such/magick");
        Start();

        var (response, _) = await GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
