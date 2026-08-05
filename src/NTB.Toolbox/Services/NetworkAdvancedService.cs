using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace NTB.Toolbox.Services;

internal static class NetworkAdvancedService
{
    private const int MaxPortCount = 256;
    private const int MaxConcurrency = 32;

    public static async Task<string> ScanPortsAsync(string host, int startPort, int endPort)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host darf nicht leer sein.");
        if (startPort is < 1 or > 65535 || endPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(startPort), "Ports müssen zwischen 1 und 65535 liegen.");
        if (endPort < startPort) throw new ArgumentException("Der Endport muss größer oder gleich dem Startport sein.");

        var count = endPort - startPort + 1;
        if (count > MaxPortCount)
            throw new ArgumentException($"Pro Scan sind maximal {MaxPortCount} Ports erlaubt.");

        var openPorts = new ConcurrentBag<int>();
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var tasks = Enumerable.Range(startPort, count).Select(async port =>
        {
            await semaphore.WaitAsync();
            try
            {
                using var client = new TcpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
                try
                {
                    await client.ConnectAsync(host.Trim(), port, timeout.Token);
                    openPorts.Add(port);
                }
                catch
                {
                    // Geschlossene und nicht erreichbare Ports werden nicht in die Ergebnisliste aufgenommen.
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        var sorted = openPorts.OrderBy(port => port).ToArray();
        var output = new StringBuilder();
        output.AppendLine($"Ziel: {host.Trim()}");
        output.AppendLine($"Bereich: {startPort}-{endPort} ({count} Ports)");
        output.AppendLine($"Offene Ports: {sorted.Length}");
        output.AppendLine();
        output.AppendLine(sorted.Length == 0 ? "Keine offenen TCP-Ports gefunden." : string.Join(Environment.NewLine, sorted.Select(port => $"TCP {port}: offen")));
        return output.ToString();
    }

    public static async Task<string> ListWlanProfilesAsync()
    {
        var result = await CommandRunner.RunAsync("netsh.exe", "wlan show profiles");
        if (!result.Success)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "WLAN-Profile konnten nicht gelesen werden." : result.StandardError);
        return result.StandardOutput;
    }

    public static async Task<string> ExportWlanProfilesAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Exportordner darf nicht leer sein.");
        Directory.CreateDirectory(folder);
        var escaped = folder.Replace("\"", "\\\"");
        var result = await CommandRunner.RunAsync("netsh.exe", $"wlan export profile folder=\"{escaped}\"");
        if (!result.Success)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? "WLAN-Profile konnten nicht exportiert werden." : result.StandardError);
        return $"WLAN-Profile wurden ohne Klartext-Schlüssel exportiert.\r\n\r\nZiel: {folder}\r\n\r\n{result.StandardOutput}";
    }
}
