using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Tyche.Service.Configuration;
using Tyche.Service.Processing;

namespace Tyche.Service.Health;

public sealed class MagickHealthCheck : IHealthCheck
{
    private readonly ImageApiOptions options;
    private readonly IProcessRunner runner;

    public MagickHealthCheck(IOptions<ImageApiOptions> options, IProcessRunner runner)
    {
        this.options = options.Value;
        this.runner = runner;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await runner.RunAsync(
                options.MagickPath,
                ["-version"],
                TimeSpan.FromSeconds(5),
                cancellationToken);
            return result.ExitCode == 0
                ? HealthCheckResult.Healthy("ImageMagick is available.")
                : HealthCheckResult.Unhealthy($"'{options.MagickPath} -version' exited with code {result.ExitCode}.");
        }
        catch (ImageProcessingException exception)
        {
            return HealthCheckResult.Unhealthy("ImageMagick is not usable.", exception);
        }
    }
}
