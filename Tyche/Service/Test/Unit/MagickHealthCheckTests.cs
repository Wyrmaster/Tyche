using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tyche.Service.Health;
using Tyche.Service.Processing;
using Tyche.Service.Test.Support;

namespace Tyche.Service.Test.Unit;

public class MagickHealthCheckTests
{
    [Fact]
    public async Task Healthy_when_magick_runs()
    {
        var check = new MagickHealthCheck(OptionsFactory.Create(), new FakeProcessRunner());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unhealthy_when_exit_code_is_not_zero()
    {
        var check = new MagickHealthCheck(OptionsFactory.Create(), new FakeProcessRunner { ExitCode = 1 });

        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
    }

    [Fact]
    public async Task Unhealthy_when_binary_cannot_be_started()
    {
        var check = new MagickHealthCheck(
            OptionsFactory.Create(options => options.MagickPath = "/no/such/magick"),
            new ProcessRunner());

        Assert.Equal(HealthStatus.Unhealthy, (await check.CheckHealthAsync(new HealthCheckContext())).Status);
    }
}
