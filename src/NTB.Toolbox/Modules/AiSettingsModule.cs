using System.Diagnostics;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class AiSettingsModule : IToolboxModule
{
    public string Id => "ai-settings";
    public string Title => "KI-Einstellungen";
    public string Category => "Einstellungen";
    public string Description => "NTB-KI-Proxy oder OpenAI API konfigurieren und die Verbindung testen.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["ki", "ai", "openai", "api", "proxy", "token", "einstellungen"];

    public Control CreateView()
    {
        var configuration = AiConfigurationService.Load();
        var enabled = new CheckBox { Text = "KI-Funktionen aktivieren", Checked = configuration.Enabled, AutoSize = true };
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        mode.Items.AddRange(["NTB AI Proxy (empfohlen)", "OpenAI API (Expertenmodus)"]);
        mode.SelectedIndex = configuration.Mode == AiProviderMode.OpenAi ? 1 : 0;
        var endpoint = new TextBox { Text = configuration.Endpoint, Width = 520, PlaceholderText = "https://server/v1/ask oder https://api.openai.com/v1/responses" };
        var secret = new TextBox { Text = configuration.Secret, Width = 520, UseSystemPasswordChar = true, PlaceholderText = "Proxy-Token oder OpenAI API-Key" };
        var showSecret = new CheckBox { Text = "Token/API-Key anzeigen", AutoSize = true };
        var model = new TextBox { Text = configuration.Model, Width = 260 };
        var timeout = new NumericUpDown { Minimum = 10, Maximum = 180, Value = Math.Clamp(configuration.TimeoutSeconds, 10, 180), Width = 100 };
        var instruction = new TextBox { Text = configuration.SystemInstruction, Multiline = true, Height = 95, Width = 620, ScrollBars = ScrollBars.Vertical };
        var status = new Label { AutoSize = true, Text = configuration.Enabled ? "Konfiguration geladen." : "KI ist deaktiviert." };
        var save = new Button { Text = "Speichern", AutoSize = true, Height = 36 };
        var test = new Button { Text = "Verbindung testen", AutoSize = true, Height = 36 };
        var reset = new Button { Text = "Zurücksetzen", AutoSize = true, Height = 36 };

        void UpdateMode()
        {
            var isOpenAi = mode.SelectedIndex == 1;
            if (isOpenAi && string.IsNullOrWhiteSpace(endpoint.Text)) endpoint.Text = "https://api.openai.com/v1/responses";
            model.Enabled = isOpenAi;
        }

        AiConfiguration ReadForm() => new()
        {
            Enabled = enabled.Checked,
            Mode = mode.SelectedIndex == 1 ? AiProviderMode.OpenAi : AiProviderMode.NtbProxy,
            Endpoint = endpoint.Text.Trim(),
            Secret = secret.Text,
            Model = string.IsNullOrWhiteSpace(model.Text) ? "gpt-5" : model.Text.Trim(),
            TimeoutSeconds = (int)timeout.Value,
            SystemInstruction = instruction.Text.Trim()
        };

        mode.SelectedIndexChanged += (_, _) => UpdateMode();
        showSecret.CheckedChanged += (_, _) => secret.UseSystemPasswordChar = !showSecret.Checked;
        save.Click += (_, _) =>
        {
            try
            {
                var value = ReadForm();
                if (value.Enabled && !Uri.TryCreate(value.Endpoint, UriKind.Absolute, out _))
                    throw new InvalidOperationException("Bitte einen gültigen HTTPS-Endpunkt eintragen.");
                AiConfigurationService.Save(value);
                status.Text = "Einstellungen sicher gespeichert.";
                AppLog.Write("KI-Einstellungen gespeichert.");
            }
            catch (Exception ex)
            {
                status.Text = "Fehler: " + ex.Message;
                AppLog.Write("KI-Einstellungen konnten nicht gespeichert werden: " + ex.Message);
            }
        };
        test.Click += async (_, _) =>
        {
            test.Enabled = false;
            status.Text = "Verbindung wird getestet …";
            try
            {
                var value = ReadForm();
                var stopwatch = Stopwatch.StartNew();
                var answer = await QuickAiService.AskAsync("Antworte ausschließlich mit: Verbindung erfolgreich", value);
                stopwatch.Stop();
                status.Text = $"Verbunden ({stopwatch.ElapsedMilliseconds} ms): {answer}";
                AppLog.Write($"KI-Verbindungstest erfolgreich ({stopwatch.ElapsedMilliseconds} ms). ");
            }
            catch (Exception ex)
            {
                status.Text = "Verbindung fehlgeschlagen: " + ex.Message;
                AppLog.Write("KI-Verbindungstest fehlgeschlagen: " + ex.Message);
            }
            finally { test.Enabled = true; }
        };
        reset.Click += (_, _) =>
        {
            enabled.Checked = false;
            mode.SelectedIndex = 0;
            endpoint.Clear();
            secret.Clear();
            model.Text = "gpt-5";
            timeout.Value = 60;
            instruction.Text = new AiConfiguration().SystemInstruction;
            status.Text = "Formular zurückgesetzt. Zum Übernehmen speichern.";
        };

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var index = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 7, 10, 0) }, 0, index);
            form.Controls.Add(control, 1, index);
        }
        Row("Aktiv", enabled);
        Row("Betriebsmodus", mode);
        Row("Endpunkt", endpoint);
        Row("Token / API-Key", secret);
        Row("", showSecret);
        Row("Modell", model);
        Row("Timeout (Sekunden)", timeout);
        Row("Systemanweisung", instruction);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([save, test, reset]);
        Row("", buttons);
        Row("Status", status);
        UpdateMode();

        var root = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        root.Controls.Add(form);
        return root;
    }
}
