using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NTB.Toolbox.Services;

internal static class NetworkDiagnosticsService
{
    public static async Task<string> RunAsync()
    {
        var lines = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up))
        {
            lines.Add($"Adapter: {nic.Name} ({nic.NetworkInterfaceType})");
            foreach (var address in nic.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
                lines.Add($"  IPv4: {address.Address}");
        }

        using var ping = new Ping();
        foreach (var host in new[] { "1.1.1.1", "8.8.8.8" })
        {
            try
            {
                var reply = await ping.SendPingAsync(host, 2000);
                lines.Add($"Ping {host}: {reply.Status} ({reply.RoundtripTime} ms)");
            }
            catch (Exception ex) { lines.Add($"Ping {host}: Fehler - {ex.Message}"); }
        }
        return string.Join(Environment.NewLine, lines);
    }
}
