using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class PortScanModule : IToolboxModule
{
    public string Id => "port-scan";
    public string Title => "TCP-Portscan";
    public string Category => "Netzwerk";
    public string Description => "Prüft einen begrenzten Bereich von maximal 256 TCP-Ports.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["ports", "scan", "tcp", "offen", "firewall"];

    public Control CreateView()
    {
        var host = new TextBox { PlaceholderText = "Host oder IP-Adresse", Width = 420 };
        var start = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 1, Width = 110 };
        var end = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 128, Width = 110 };
        var run = new Button { Text = "Scan starten", AutoSize = true, Height = 36 };
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };

        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            output.Text = "Portscan läuft …";
            AppLog.Write($"TCP-Portscan gestartet: {host.Text}:{start.Value}-{end.Value}");
            try
            {
                output.Text = await NetworkAdvancedService.ScanPortsAsync(host.Text, (int)start.Value, (int)end.Value);
                AppLog.Write("TCP-Portscan abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write("TCP-Portscan fehlgeschlagen: " + ex.Message);
            }
            finally
            {
                run.Enabled = true;
            }
        };

        var inputs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(8), WrapContents = true };
        inputs.Controls.Add(new Label { Text = "Ziel", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        inputs.Controls.Add(host);
        inputs.Controls.Add(new Label { Text = "Von", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
        inputs.Controls.Add(start);
        inputs.Controls.Add(new Label { Text = "Bis", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
        inputs.Controls.Add(end);
        inputs.Controls.Add(run);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(inputs);
        return panel;
    }
}

internal sealed class WlanProfilesModule : IToolboxModule
{
    public string Id => "wlan-profiles";
    public string Title => "WLAN-Profile";
    public string Category => "Netzwerk";
    public string Description => "Zeigt gespeicherte WLAN-Profile und exportiert sie ohne Klartext-Schlüssel.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["wlan", "wifi", "profile", "ssid", "export"];

    public Control CreateView()
    {
        var show = new Button { Text = "Profile anzeigen", AutoSize = true, Height = 36 };
        var export = new Button { Text = "Profile exportieren", AutoSize = true, Height = 36 };
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };

        show.Click += async (_, _) =>
        {
            show.Enabled = false;
            output.Text = "WLAN-Profile werden gelesen …";
            try
            {
                output.Text = await NetworkAdvancedService.ListWlanProfilesAsync();
                AppLog.Write("WLAN-Profile angezeigt.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write("WLAN-Profile konnten nicht gelesen werden: " + ex.Message);
            }
            finally
            {
                show.Enabled = true;
            }
        };

        export.Click += async (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Exportordner für WLAN-Profile auswählen", UseDescriptionForTitle = true };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            export.Enabled = false;
            output.Text = "WLAN-Profile werden exportiert …";
            try
            {
                output.Text = await NetworkAdvancedService.ExportWlanProfilesAsync(dialog.SelectedPath);
                AppLog.Write("WLAN-Profile exportiert: " + dialog.SelectedPath);
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write("WLAN-Export fehlgeschlagen: " + ex.Message);
            }
            finally
            {
                export.Enabled = true;
            }
        };

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(8) };
        actions.Controls.Add(show);
        actions.Controls.Add(export);
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(actions);
        return panel;
    }
}
