using System.Diagnostics;
using NTB.Toolbox.Models;

namespace NTB.Toolbox.Services;

internal static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken token = default)
    {
        var output = new List<string>();
        var error = new List<string>();
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.Add(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(token);
        return new CommandResult(process.ExitCode == 0, process.ExitCode, string.Join(Environment.NewLine, output), string.Join(Environment.NewLine, error));
    }
}
