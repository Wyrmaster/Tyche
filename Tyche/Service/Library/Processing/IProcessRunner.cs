namespace Tyche.Service.Processing;

public interface IProcessRunner
{
  /// <summary>Starts a program without a shell. Throws <see cref="ImageProcessingException"/> on start failure or timeout.</summary>
  Task<ProcessResult> RunAsync(
    string program,
    IReadOnlyList<string> arguments,
    TimeSpan timeout,
    CancellationToken cancellationToken);
}
