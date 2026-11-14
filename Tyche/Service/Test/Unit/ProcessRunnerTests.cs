using Tyche.Service.Processing;

namespace Tyche.Service.Test.Unit;

public class ProcessRunnerTests
{
    private readonly ProcessRunner runner = new();

    [Fact]
    public async Task Arguments_are_not_interpreted_by_a_shell()
    {
        var result = await runner.RunAsync(
            "/bin/echo",
            ["a b", "$(id)", "; rm -rf /", "*"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("a b $(id) ; rm -rf / *", result.StandardOutput.Trim());
    }

    [Fact]
    public async Task Exit_code_and_stderr_are_captured()
    {
        var result = await runner.RunAsync(
            "/bin/sh",
            ["-c", "echo oops >&2; exit 3"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("oops", result.StandardError);
    }

    [Fact]
    public async Task Missing_program_throws_processing_exception()
    {
        await Assert.ThrowsAsync<ImageProcessingException>(() => runner.RunAsync(
            "/definitely/not/here",
            [],
            TimeSpan.FromSeconds(5),
            CancellationToken.None));
    }

    [Fact]
    public async Task Timeout_kills_the_process_and_throws()
    {
        var started = DateTime.UtcNow;

        var exception = await Assert.ThrowsAsync<ImageProcessingException>(() => runner.RunAsync(
            "/bin/sleep",
            ["30"],
            TimeSpan.FromMilliseconds(300),
            CancellationToken.None));

        Assert.Contains("did not finish", exception.Message);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Timeout_with_a_child_holding_the_pipes_still_returns_promptly()
    {
        var started = DateTime.UtcNow;

        await Assert.ThrowsAsync<ImageProcessingException>(() => runner.RunAsync(
            "/bin/sh",
            ["-c", "sleep 30 & sleep 30"],
            TimeSpan.FromMilliseconds(300),
            CancellationToken.None));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Caller_cancellation_propagates_as_cancellation()
    {
        using var cancel = new CancellationTokenSource(200);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            "/bin/sleep",
            ["30"],
            TimeSpan.FromSeconds(30),
            cancel.Token));
    }

    [Fact]
    public async Task Huge_output_is_truncated()
    {
        var result = await runner.RunAsync(
            "/bin/sh",
            ["-c", "head -c 1000000 /dev/zero | tr '\\0' 'a'"],
            TimeSpan.FromSeconds(10),
            CancellationToken.None);

        Assert.True(result.StandardOutput.Length <= 4096);
    }
}
