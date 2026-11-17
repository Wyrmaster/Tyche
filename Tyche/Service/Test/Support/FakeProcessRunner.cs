using Tyche.Service.Processing;

namespace Tyche.Service.Test.Support;

public sealed class FakeProcessRunner : IProcessRunner
{
  private int _running;
  private int _maxRunning;

  public List<IReadOnlyList<string>> Calls { get; } = [];

  public TimeSpan Delay { get; set; } = TimeSpan.Zero;

  public int ExitCode { get; set; }

  public int MaxConcurrent => _maxRunning;

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

    var now = Interlocked.Increment(ref _running);
    int seen;
    while (now > (seen = _maxRunning))
    {
      Interlocked.CompareExchange(ref _maxRunning, now, seen);
    }

    try
    {
      await Task.Delay(Delay, cancellationToken);
      return new ProcessResult(ExitCode, string.Empty, ExitCode == 0 ? string.Empty : "boom");
    }
    finally
    {
      Interlocked.Decrement(ref _running);
    }
  }
}
