using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NTB.Toolbox.Services;

internal static class NetworkToolService
{
    public static async Task<string> PingAsync(string host, int count = 4)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host darf nicht leer sein.");
        using var ping = new Ping();
        var times = new List<long>();
        var output = new StringBuilder();

        for (var i = 1; i <= count; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(host.Trim(), 3000);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                    output.AppendLine($"{i}: {reply.Address}  {reply.RoundtripTime} ms  TTL={reply.Options?.Ttl}");
                }
                else
                {
                    output.AppendLine($"{i}: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"{i}: Fehler: {ex.Message}");
            }
        }

        output.AppendLine();
        output.AppendLine($"Gesendet: {count}, Empfangen: {times.Count}, Verloren: {count - times.Count}");
        if (times.Count > 0)
            output.AppendLine($"Minimum: {times.Min()} ms, Maximum: {times.Max()} ms, Mittelwert: {times.Average():0.0} ms");
        return output.ToString();
    }

    public static async Task<string> TraceRouteAsync(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host darf nicht leer sein.");
        using var ping = new Ping();
        var buffer = Encoding.ASCII.GetBytes("NTB Toolbox traceroute");
        var output = new StringBuilder();

        for (var ttl = 1; ttl <= 30; ttl++)
        {
            var options = new PingOptions(ttl, true);
            PingReply reply;
            try
            {
                reply = await ping.SendPingAsync(host.Trim(), 3500, buffer, options);
            }
            catch (Exception ex)
            {
                output.AppendLine($"{ttl,2}  Fehler: {ex.Message}");
                break;
            }

            var address = reply.Address?.ToString() ?? "*";
            output.AppendLine($"{ttl,2}  {address,-40}  {reply.RoundtripTime,5} ms  {reply.Status}");
            if (reply.Status == IPStatus.Success) break;
        }

        return output.ToString();
    }

    public static async Task<string> DnsLookupAsync(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host darf nicht leer sein.");
        var entry = await Dns.GetHostEntryAsync(host.Trim());
        var output = new StringBuilder();
        output.AppendLine($"Hostname: {entry.HostName}");
        if (entry.Aliases.Length > 0) output.AppendLine($"Aliase: {string.Join(", ", entry.Aliases)}");
        output.AppendLine("Adressen:");
        foreach (var address in entry.AddressList)
            output.AppendLine($"  {address} ({address.AddressFamily})");
        return output.ToString();
    }

    public static async Task<string> TestTcpPortAsync(string host, int port)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host darf nicht leer sein.");
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port muss zwischen 1 und 65535 liegen.");

        using var client = new TcpClient();
        var started = DateTime.UtcNow;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await client.ConnectAsync(host.Trim(), port, timeout.Token);
            var elapsed = DateTime.UtcNow - started;
            return $"TCP-Verbindung zu {host.Trim()}:{port} erfolgreich.\r\nAntwortzeit: {elapsed.TotalMilliseconds:0} ms";
        }
        catch (OperationCanceledException)
        {
            return $"TCP-Verbindung zu {host.Trim()}:{port} nach 5 Sekunden abgebrochen.";
        }
        catch (Exception ex)
        {
            return $"TCP-Verbindung zu {host.Trim()}:{port} fehlgeschlagen.\r\n{ex.Message}";
        }
    }

    public static async Task SendWakeOnLanAsync(string macAddress, string broadcastAddress = "255.255.255.255", int port = 9)
    {
        var clean = new string(macAddress.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length != 12) throw new ArgumentException("Ungültige MAC-Adresse.");
        var mac = Enumerable.Range(0, 6).Select(i => Convert.ToByte(clean.Substring(i * 2, 2), 16)).ToArray();
        var packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);
        for (var i = 6; i < packet.Length; i += 6) Buffer.BlockCopy(mac, 0, packet, i, 6);

        using var udp = new UdpClient { EnableBroadcast = true };
        await udp.SendAsync(packet, packet.Length, broadcastAddress, port);
    }
}
