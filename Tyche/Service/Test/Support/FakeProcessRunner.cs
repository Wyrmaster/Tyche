using Tyche.Service.Processing;

namespace Tyche.Service.Test.Support;

public sealed class FakeProcessRunner : IProcessRunner
{
    private int running;
    private int maxRunning;

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int ExitCode { get; set; }

    public int MaxConcurrent => maxRunning;

    public async Task<ProcessResult> RunAsync(
        string program,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        lock (Calls)
        {
            Calls.Add(arguments);
        }

        var now = Interlocked.Increment(ref running);
        int seen;
        while (now > (seen = maxRunning))
        {
            Interlocked.CompareExchange(ref maxRunning, now, seen);
        }

        try
        {
            await Task.Delay(Delay, cancellationToken);
            return new ProcessResult(ExitCode, string.Empty, ExitCode == 0 ? string.Empty : "boom");
        }
        finally
        {
            Interlocked.Decrement(ref running);
        }
    }
}
