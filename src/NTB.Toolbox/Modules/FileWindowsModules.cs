using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class ConfirmedTaskModule : IToolboxModule
{
    private readonly string _buttonText;
    private readonly string _confirmation;
    private readonly Func<Task<string>> _run;

    public ConfirmedTaskModule(string id, string title, string category, string description, IReadOnlyCollection<string> keywords, bool requiresAdministrator, string buttonText, string confirmation, Func<Task<string>> run)
    {
        Id = id;
        Title = title;
        Category = category;
        Description = description;
        Keywords = keywords;
        RequiresAdministrator = requiresAdministrator;
        _buttonText = buttonText;
        _confirmation = confirmation;
        _run = run;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public bool RequiresAdministrator { get; }
    public IReadOnlyCollection<string> Keywords { get; }

    public Control CreateView()
    {
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10), Text = Description };
        var run = new Button { Text = _buttonText, Dock = DockStyle.Top, Height = 38 };
        run.Click += async (_, _) =>
        {
            if (MessageBox.Show(_confirmation, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            run.Enabled = false;
            output.Text = "Wird ausgeführt …";
            AppLog.Write($"{Title} gestartet.");
            try
            {
                output.Text = await _run();
                AppLog.Write($"{Title} abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write($"{Title} fehlgeschlagen: {ex.Message}");
            }
            finally
            {
                run.Enabled = true;
            }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(run);
        return panel;
    }
}

internal sealed class FileHashModule : IToolboxModule
{
    public string Id => "file-hashes";
    public string Title => "Dateihashes";
    public string Category => "Dateien";
    public string Description => "Berechnet SHA-256 und SHA-512 für eine ausgewählte Datei.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["hash", "sha256", "sha512", "checksum", "prüfsumme"];

    public Control CreateView()
    {
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10), Text = Description };
        var select = new Button { Text = "Datei auswählen und Hashes berechnen", Dock = DockStyle.Top, Height = 38 };
        select.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Datei für Hash-Berechnung auswählen", CheckFileExists = true };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            select.Enabled = false;
            output.Text = "Hashes werden berechnet …";
            AppLog.Write($"Hash-Berechnung gestartet: {dialog.FileName}");
            try
            {
                output.Text = await FileWindowsToolService.CalculateHashesAsync(dialog.FileName);
                AppLog.Write("Hash-Berechnung abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write($"Hash-Berechnung fehlgeschlagen: {ex.Message}");
            }
            finally
            {
                select.Enabled = true;
            }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(select);
        return panel;
    }
}
