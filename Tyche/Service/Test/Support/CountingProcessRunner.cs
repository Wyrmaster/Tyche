using Tyche.Service.Processing;

namespace Tyche.Service.Test.Support;

/// <summary>Wraps the real runner and records every call, so tests can prove ImageMagick was (not) called.</summary>
public sealed class CountingProcessRunner : IProcessRunner
{
  private readonly ProcessRunner _inner = new();
  private int _conversions;

  /// <summary>Number of image conversions (calls other than the health check's -version).</summary>
  public int Conversions => _conversions;

  public Task<ProcessResult> RunAsync(
    string program,
    IReadOnlyList<string> arguments,
    TimeSpan timeout,
    CancellationToken cancellationToken)
  {
    if (!arguments.Contains("-version"))
    {
      Interlocked.Increment(ref _conversions);
    }

    return _inner.RunAsync(program, arguments, timeout, cancellationToken);
  }
}
