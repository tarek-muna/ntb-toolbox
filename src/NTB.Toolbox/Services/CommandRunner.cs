using System.Collections.Concurrent;
using System.Diagnostics;
using NTB.Toolbox.Models;

namespace NTB.Toolbox.Services;

internal static class CommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken token = default,
        TimeSpan? timeout = null)
    {
        var output = new ConcurrentQueue<string>();
        var error = new ConcurrentQueue<string>();
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.Enqueue(e.Data); };

        try
        {
            if (!process.Start())
                return new CommandResult(false, -1, string.Empty, $"{fileName} konnte nicht gestartet werden.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutSource = new CancellationTokenSource(timeout ?? DefaultTimeout);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);

            try
            {
                await process.WaitForExitAsync(linkedSource.Token);
                process.WaitForExit();
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                var reason = token.IsCancellationRequested
                    ? "Der Vorgang wurde abgebrochen."
                    : $"Zeitüberschreitung nach {(timeout ?? DefaultTimeout).TotalMinutes:0.#} Minuten.";
                return new CommandResult(false, -2, string.Join(Environment.NewLine, output), reason);
            }

            return new CommandResult(
                process.ExitCode == 0,
                process.ExitCode,
                string.Join(Environment.NewLine, output),
                string.Join(Environment.NewLine, error));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new CommandResult(false, -1, string.Join(Environment.NewLine, output), $"{fileName} konnte nicht ausgeführt werden: {ex.Message}");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Der Prozess ist eventuell bereits beendet oder darf nicht beendet werden.
        }
    }
}
