namespace NTB.Toolbox.Models;

internal sealed record CommandResult(bool Success, int ExitCode, string StandardOutput, string StandardError);
