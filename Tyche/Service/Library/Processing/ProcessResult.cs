namespace Tyche.Service.Processing;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
