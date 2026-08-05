using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class OsiAnalysisModule : IToolboxModule
{
    public string Id => "osi-analysis";
    public string Title => "OSI-Analyse";
    public string Category => "Netzwerk";
    public string Description => "Analysiert ein Ziel entlang der sieben OSI-Schichten und zeigt Fehlerursachen sowie Handlungshinweise.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["osi", "schichten", "layer", "netzwerk", "diagnose", "tls", "http", "tcp"];

    public Control CreateView()
    {
        var target = new TextBox { Width = 260, Text = "example.com", PlaceholderText = "Hostname oder IP-Adresse" };
        var port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 443, Width = 90 };
        var tls = new CheckBox { Text = "TLS/HTTPS prüfen", Checked = true, AutoSize = true, Padding = new Padding(8, 7, 0, 0) };
        var run = new Button { Text = "OSI-Analyse starten", AutoSize = true, Height = 32 };
        var export = new Button { Text = "Bericht exportieren", AutoSize = true, Height = 32, Enabled = false };
        var status = new Label { Text = "Bereit", AutoSize = true, Padding = new Padding(8, 8, 0, 0) };
        var results = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(4) };
        IReadOnlyList<OsiLayerResult>? latest = null;

        var input = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        input.Controls.Add(new Label { Text = "Ziel:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
        input.Controls.Add(target);
        input.Controls.Add(new Label { Text = "Port:", AutoSize = true, Padding = new Padding(12, 8, 4, 0) });
        input.Controls.Add(port);
        input.Controls.Add(tls);
        input.Controls.Add(run);
        input.Controls.Add(export);
        input.Controls.Add(status);

        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            export.Enabled = false;
            status.Text = "Analyse läuft …";
            results.Controls.Clear();
            AppLog.Write($"OSI-Analyse für {target.Text}:{port.Value} gestartet.");

            try
            {
                latest = await OsiAnalysisService.AnalyzeAsync(target.Text, (int)port.Value, tls.Checked);
                foreach (var layer in latest.OrderBy(x => x.Layer))
                    results.Controls.Add(CreateLayerCard(layer, results.ClientSize.Width - 36));
                export.Enabled = true;
                var failures = latest.Count(x => x.Status == OsiStatus.Failed);
                status.Text = failures == 0 ? "Analyse abgeschlossen" : $"{failures} Fehler erkannt";
                AppLog.Write($"OSI-Analyse abgeschlossen: {failures} Fehler.");
            }
            catch (Exception ex)
            {
                status.Text = "Analyse fehlgeschlagen";
                results.Controls.Add(new Label { Text = ex.Message, AutoSize = true, ForeColor = Color.Firebrick });
                AppLog.Write($"OSI-Analyse fehlgeschlagen: {ex.Message}");
            }
            finally
            {
                run.Enabled = true;
            }
        };

        export.Click += (_, _) =>
        {
            if (latest is null) return;
            using var dialog = new SaveFileDialog
            {
                Filter = "Textdatei (*.txt)|*.txt",
                FileName = $"NTB-OSI-Analyse-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            File.WriteAllText(dialog.FileName, BuildReport(target.Text, (int)port.Value, tls.Checked, latest));
            AppLog.Write($"OSI-Bericht exportiert: {dialog.FileName}");
            MessageBox.Show("Der OSI-Bericht wurde gespeichert.", "NTB Toolbox", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(results);
        panel.Controls.Add(input);
        return panel;
    }

    private static Control CreateLayerCard(OsiLayerResult result, int width)
    {
        var icon = result.Status switch
        {
            OsiStatus.Success => "✓",
            OsiStatus.Warning => "!",
            OsiStatus.Failed => "✕",
            _ => "–"
        };
        var color = result.Status switch
        {
            OsiStatus.Success => Color.ForestGreen,
            OsiStatus.Warning => Color.DarkOrange,
            OsiStatus.Failed => Color.Firebrick,
            _ => Color.DimGray
        };

        var card = new Panel { Width = Math.Max(520, width), Height = 142, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(12), BorderStyle = BorderStyle.FixedSingle };
        var title = new Label { Text = $"{icon}  Schicht {result.Layer}: {result.Name} — {result.Summary}", AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = color, Location = new Point(12, 10) };
        var details = new Label { Text = result.Details, AutoSize = true, MaximumSize = new Size(card.Width - 28, 48), Location = new Point(14, 42) };
        var recommendation = new Label { Text = "Hinweis: " + result.Recommendation, AutoSize = true, MaximumSize = new Size(card.Width - 28, 44), ForeColor = Color.DimGray, Location = new Point(14, 94) };
        card.Controls.Add(title);
        card.Controls.Add(details);
        card.Controls.Add(recommendation);
        return card;
    }

    private static string BuildReport(string target, int port, bool tls, IReadOnlyList<OsiLayerResult> results)
    {
        var lines = new List<string>
        {
            "NTB Toolbox - OSI-Analyse",
            $"Zeitpunkt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Ziel: {target}:{port}",
            $"TLS: {(tls ? "Ja" : "Nein")}",
            new string('=', 72)
        };

        foreach (var result in results.OrderBy(x => x.Layer))
        {
            lines.Add($"Schicht {result.Layer} - {result.Name} [{result.Status}]");
            lines.Add(result.Summary);
            lines.Add(result.Details);
            lines.Add("Empfehlung: " + result.Recommendation);
            lines.Add(new string('-', 72));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
