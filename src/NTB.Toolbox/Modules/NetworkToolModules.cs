using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class NetworkInputModule : IToolboxModule
{
    private readonly Func<string, Task<string>> _run;
    private readonly string _placeholder;

    public NetworkInputModule(string id, string title, string description, IReadOnlyCollection<string> keywords, string placeholder, Func<string, Task<string>> run)
    {
        Id = id;
        Title = title;
        Description = description;
        Keywords = keywords;
        _placeholder = placeholder;
        _run = run;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category => "Netzwerk";
    public string Description { get; }
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords { get; }

    public Control CreateView()
    {
        var input = new TextBox { Dock = DockStyle.Top, Height = 34, PlaceholderText = _placeholder };
        var run = new Button { Text = "Ausführen", Dock = DockStyle.Top, Height = 38 };
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            output.Text = "Wird ausgeführt …";
            AppLog.Write($"{Title} gestartet: {input.Text}");
            try
            {
                output.Text = await _run(input.Text);
                AppLog.Write($"{Title} abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write($"{Title} fehlgeschlagen: {ex.Message}");
            }
            finally { run.Enabled = true; }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(run);
        panel.Controls.Add(input);
        return panel;
    }
}

internal sealed class TcpPortModule : IToolboxModule
{
    public string Id => "tcp-port-test";
    public string Title => "TCP-Porttest";
    public string Category => "Netzwerk";
    public string Description => "Prüft, ob ein TCP-Port auf einem Zielsystem erreichbar ist.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["tcp", "port", "socket", "erreichbarkeit"];

    public Control CreateView()
    {
        var host = new TextBox { Width = 320, PlaceholderText = "Host oder IP-Adresse" };
        var port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 443, Width = 100 };
        var run = new Button { Text = "Port prüfen", AutoSize = true };
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };
        var inputs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        inputs.Controls.Add(host);
        inputs.Controls.Add(port);
        inputs.Controls.Add(run);

        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            output.Text = "Wird geprüft …";
            AppLog.Write($"TCP-Porttest gestartet: {host.Text}:{port.Value}");
            try { output.Text = await NetworkToolService.TestTcpPortAsync(host.Text, (int)port.Value); }
            catch (Exception ex) { output.Text = "Fehler: " + ex.Message; }
            finally { run.Enabled = true; }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(inputs);
        return panel;
    }
}

internal sealed class WakeOnLanModule : IToolboxModule
{
    public string Id => "wake-on-lan";
    public string Title => "Wake-on-LAN";
    public string Category => "Netzwerk";
    public string Description => "Sendet ein Magic Packet an eine MAC-Adresse.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["wol", "wake", "magic packet", "mac"];

    public Control CreateView()
    {
        var mac = new TextBox { Width = 220, PlaceholderText = "MAC-Adresse" };
        var broadcast = new TextBox { Width = 180, Text = "255.255.255.255" };
        var send = new Button { Text = "Magic Packet senden", AutoSize = true };
        var status = new Label { AutoSize = true, Padding = new Padding(0, 10, 0, 0) };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { Text = "MAC-Adresse des Zielgeräts", AutoSize = true });
        layout.Controls.Add(mac);
        layout.Controls.Add(new Label { Text = "Broadcast-Adresse", AutoSize = true });
        layout.Controls.Add(broadcast);
        layout.Controls.Add(send);
        layout.Controls.Add(status);

        send.Click += async (_, _) =>
        {
            send.Enabled = false;
            try
            {
                await NetworkToolService.SendWakeOnLanAsync(mac.Text, broadcast.Text);
                status.Text = "Magic Packet wurde gesendet.";
                AppLog.Write($"Wake-on-LAN gesendet: {mac.Text}");
            }
            catch (Exception ex)
            {
                status.Text = "Fehler: " + ex.Message;
                AppLog.Write($"Wake-on-LAN fehlgeschlagen: {ex.Message}");
            }
            finally { send.Enabled = true; }
        };
        return layout;
    }
}
