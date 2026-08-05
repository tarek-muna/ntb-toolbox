using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace NTB.Toolbox.Services;

internal enum OsiStatus
{
    Success,
    Warning,
    Failed,
    Skipped
}

internal sealed record OsiLayerResult(int Layer, string Name, OsiStatus Status, string Summary, string Details, string Recommendation);

internal static class OsiAnalysisService
{
    public static async Task<IReadOnlyList<OsiLayerResult>> AnalyzeAsync(string target, int port, bool useTls)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Ziel darf nicht leer sein.");
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Port muss zwischen 1 und 65535 liegen.");

        target = target.Trim();
        var results = new List<OsiLayerResult>();

        var activeAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                              adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .ToList();

        results.Add(activeAdapters.Count > 0
            ? new OsiLayerResult(1, "Bitübertragung", OsiStatus.Success, $"{activeAdapters.Count} aktiver Netzwerkadapter", string.Join(Environment.NewLine, activeAdapters.Select(a => $"{a.Name}: {a.NetworkInterfaceType}, {FormatSpeed(a.Speed)}")), "Keine Maßnahme erforderlich.")
            : new OsiLayerResult(1, "Bitübertragung", OsiStatus.Failed, "Kein aktiver Netzwerkadapter", "Windows meldet keinen aktiven physischen Netzwerkadapter.", "Kabel, WLAN-Schalter, Treiber und Adapterstatus prüfen."));

        var adapterDetails = activeAdapters.Select(adapter =>
        {
            var properties = adapter.GetIPProperties();
            var mac = adapter.GetPhysicalAddress().ToString();
            return $"{adapter.Name}: MAC {FormatMac(mac)}, MTU {properties.GetIPv4Properties()?.Mtu.ToString() ?? "n/a"}";
        }).ToList();
        results.Add(activeAdapters.Count > 0
            ? new OsiLayerResult(2, "Sicherung", OsiStatus.Success, "Link-Layer verfügbar", string.Join(Environment.NewLine, adapterDetails), "Bei Paketverlust Switch-Port, WLAN-Signal und Duplex prüfen.")
            : new OsiLayerResult(2, "Sicherung", OsiStatus.Skipped, "Nicht geprüft", "Ohne aktiven Adapter ist keine Sicherungsschicht verfügbar.", "Zuerst Schicht 1 beheben."));

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(target);
            var gateways = activeAdapters.SelectMany(a => a.GetIPProperties().GatewayAddresses).Select(g => g.Address).Where(a => !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any)).Distinct().ToList();
            results.Add(new OsiLayerResult(3, "Vermittlung", OsiStatus.Success, $"Ziel aufgelöst: {addresses.Length} Adresse(n)", $"Ziel: {string.Join(", ", addresses.Select(a => a.ToString()))}{Environment.NewLine}Gateway: {(gateways.Count == 0 ? "nicht erkannt" : string.Join(", ", gateways))}", gateways.Count == 0 ? "Standardgateway und Routing prüfen." : "Routing ist grundsätzlich vorhanden."));
        }
        catch (Exception ex)
        {
            addresses = [];
            results.Add(new OsiLayerResult(3, "Vermittlung", OsiStatus.Failed, "DNS/IP-Auflösung fehlgeschlagen", ex.Message, "DNS-Server, Zielnamen und IP-Konfiguration prüfen."));
        }

        if (addresses.Length > 0)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, 3000);
                var status = reply.Status == IPStatus.Success ? OsiStatus.Success : OsiStatus.Warning;
                results.Add(new OsiLayerResult(4, "Transport", status, reply.Status == IPStatus.Success ? $"ICMP erreichbar ({reply.RoundtripTime} ms)" : $"ICMP: {reply.Status}", $"Adresse: {reply.Address}{Environment.NewLine}TTL: {reply.Options?.Ttl}", reply.Status == IPStatus.Success ? "TCP-Portprüfung wird zusätzlich ausgeführt." : "ICMP kann gefiltert sein; TCP-Ergebnis beachten."));
            }
            catch (Exception ex)
            {
                results.Add(new OsiLayerResult(4, "Transport", OsiStatus.Warning, "ICMP-Prüfung fehlgeschlagen", ex.Message, "Firewall und Erreichbarkeit prüfen; TCP kann trotzdem funktionieren."));
            }
        }
        else
        {
            results.Add(new OsiLayerResult(4, "Transport", OsiStatus.Skipped, "Nicht geprüft", "Ziel konnte nicht aufgelöst werden.", "Zuerst Schicht 3 beheben."));
        }

        TcpClient? tcpClient = null;
        try
        {
            tcpClient = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var started = DateTime.UtcNow;
            await tcpClient.ConnectAsync(target, port, timeout.Token);
            var elapsed = DateTime.UtcNow - started;
            results[3] = new OsiLayerResult(4, "Transport", OsiStatus.Success, $"TCP {port} erreichbar ({elapsed.TotalMilliseconds:0} ms)", $"Remote-Endpunkt: {tcpClient.Client.RemoteEndPoint}{Environment.NewLine}Lokal: {tcpClient.Client.LocalEndPoint}", "Transportverbindung erfolgreich.");
        }
        catch (Exception ex)
        {
            results[3] = new OsiLayerResult(4, "Transport", OsiStatus.Failed, $"TCP {port} nicht erreichbar", ex.Message, "Dienst, Firewall, Portfreigabe und Zieladresse prüfen.");
        }

        if (tcpClient?.Connected == true)
        {
            if (useTls)
            {
                try
                {
                    using var ssl = new SslStream(tcpClient.GetStream(), false, (_, _, _, errors) => errors == SslPolicyErrors.None);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = target,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                    }, timeout.Token);

                    results.Add(new OsiLayerResult(5, "Sitzung", OsiStatus.Success, "TLS-Sitzung aufgebaut", $"Protokoll: {ssl.SslProtocol}{Environment.NewLine}Cipher: {ssl.NegotiatedCipherSuite}", "Sitzungsaufbau erfolgreich."));
                    results.Add(new OsiLayerResult(6, "Darstellung", OsiStatus.Success, "Verschlüsselung aktiv", $"Zertifikat: {ssl.RemoteCertificate?.Subject ?? "nicht verfügbar"}", "Zertifikatskette bei Warnungen separat prüfen."));
                }
                catch (Exception ex)
                {
                    results.Add(new OsiLayerResult(5, "Sitzung", OsiStatus.Failed, "TLS-Sitzung fehlgeschlagen", ex.Message, "TLS-Version, Zertifikat, SNI und Serverkonfiguration prüfen."));
                    results.Add(new OsiLayerResult(6, "Darstellung", OsiStatus.Skipped, "Nicht geprüft", "Ohne TLS-Sitzung keine Darstellungsprüfung.", "Zuerst Schicht 5 beheben."));
                }
            }
            else
            {
                results.Add(new OsiLayerResult(5, "Sitzung", OsiStatus.Success, "TCP-Sitzung aktiv", "Die Verbindung wurde ohne TLS geprüft.", "Für HTTPS/TLS die TLS-Option aktivieren."));
                results.Add(new OsiLayerResult(6, "Darstellung", OsiStatus.Skipped, "Nicht geprüft", "Keine TLS-/Kodierungsprüfung angefordert.", "Bei verschlüsselten Diensten TLS aktivieren."));
            }
        }
        else
        {
            results.Add(new OsiLayerResult(5, "Sitzung", OsiStatus.Skipped, "Nicht geprüft", "Keine Transportverbindung.", "Zuerst Schicht 4 beheben."));
            results.Add(new OsiLayerResult(6, "Darstellung", OsiStatus.Skipped, "Nicht geprüft", "Keine Sitzung verfügbar.", "Zuerst Schicht 4 und 5 beheben."));
        }

        try
        {
            var scheme = useTls ? "https" : "http";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(7) };
            using var request = new HttpRequestMessage(HttpMethod.Head, $"{scheme}://{target}:{port}/");
            using var response = await client.SendAsync(request);
            results.Add(new OsiLayerResult(7, "Anwendung", response.IsSuccessStatusCode ? OsiStatus.Success : OsiStatus.Warning, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", $"Server: {string.Join(", ", response.Headers.Server.Select(x => x.ToString()))}", response.IsSuccessStatusCode ? "Anwendungsdienst antwortet." : "HTTP-Status und Serveranwendung prüfen."));
        }
        catch (Exception ex)
        {
            results.Add(new OsiLayerResult(7, "Anwendung", OsiStatus.Failed, "HTTP-Anfrage fehlgeschlagen", ex.Message, "Protokoll, Port, Proxy und Anwendungsdienst prüfen."));
        }

        tcpClient?.Dispose();
        return results;
    }

    private static string FormatSpeed(long bitsPerSecond) => bitsPerSecond <= 0 ? "unbekannte Geschwindigkeit" : $"{bitsPerSecond / 1_000_000d:0.#} Mbit/s";
    private static string FormatMac(string value) => value.Length == 12 ? string.Join(":", Enumerable.Range(0, 6).Select(i => value.Substring(i * 2, 2))) : value;
}
