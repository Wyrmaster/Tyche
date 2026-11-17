using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Tyche.Service.Processing;

public sealed class ProcessRunner : IProcessRunner
{
  private const int MaxCapturedChars = 4096;

  public async Task<ProcessResult> RunAsync(
    string program,
    IReadOnlyList<string> arguments,
    TimeSpan timeout,
    CancellationToken cancellationToken)
  {
    var startInfo = new ProcessStartInfo(program)
    {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      RedirectStandardInput = true,
      CreateNoWindow = true,
    };
    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    using var process = new Process();
    process.StartInfo = startInfo;
    try
    {
      process.Start();
    }
    catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
    {
      throw new ImageProcessingException($"Could not start '{program}': {exception.Message}", exception);
    }

    process.StandardInput.Close();
    var output = ReadLimitedAsync(process.StandardOutput);
    var error = ReadLimitedAsync(process.StandardError);

    using var timeoutSource = new CancellationTokenSource(timeout);
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
    try
    {
      await process.WaitForExitAsync(linked.Token);
      return new ProcessResult(process.ExitCode, await output, await error);
    }
    catch (OperationCanceledException)
    {
      Kill(process);
      await DrainAsync(output, error);
      if (cancellationToken.IsCancellationRequested)
      {
        throw;
      }

      throw new ImageProcessingException($"'{program}' did not finish within {timeout.TotalSeconds:0} s.");
    }
  }

  private static async Task DrainAsync(Task<string> output, Task<string> error)
  {
    try
    {
      await Task.WhenAll(output, error);
    }
    catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
    {
      // pipes closed by the kill
    }
  }

  private static void Kill(Process process)
  {
    try
    {
      process.Kill(entireProcessTree: true);
    }
    catch (InvalidOperationException)
    {
      // already exited
    }
  }

  private static async Task<string> ReadLimitedAsync(StreamReader reader)
  {
    var builder = new StringBuilder();
    var buffer = new char[1024];
    int read;
    try
    {
      while ((read = await reader.ReadAsync(buffer)) > 0)
      {
        if (builder.Length < MaxCapturedChars)
        {
          builder.Append(buffer, 0, Math.Min(read, MaxCapturedChars - builder.Length));
        }
      }
    }
    catch (IOException)
    {
      // pipe closed because the process was killed
    }

    return builder.ToString();
  }
}
