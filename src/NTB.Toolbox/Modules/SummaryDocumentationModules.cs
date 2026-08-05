using System.Text;
using System.Text.RegularExpressions;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class SummaryModule : IToolboxModule
{
    public string Id => "summary";
    public string Title => "Zusammenfassung";
    public string Category => "Dokumentation";
    public string Description => "Fasst Texte und Protokolle lokal als Kurzfassung, Stichpunkte oder Maßnahmenliste zusammen.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["zusammenfassung", "text", "log", "bericht", "maßnahmen", "stichpunkte"];

    public Control CreateView()
    {
        var source = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, AcceptsReturn = true, AcceptsTab = true };
        var result = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill };
        var mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
        mode.Items.AddRange(["Kurzfassung", "Stichpunkte", "Maßnahmenliste"]);
        mode.SelectedIndex = 0;
        var summarize = new Button { Text = "Zusammenfassen", AutoSize = true };
        var clipboard = new Button { Text = "Zwischenablage einfügen", AutoSize = true };
        var copy = new Button { Text = "Ergebnis kopieren", AutoSize = true };
        var openFile = new Button { Text = "Text/Log öffnen", AutoSize = true };

        summarize.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(source.Text))
            {
                MessageBox.Show("Bitte zuerst Text oder ein Protokoll einfügen.", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            result.Text = LocalSummarizer.Summarize(source.Text, mode.SelectedItem?.ToString() ?? "Kurzfassung");
            AppLog.Write($"Zusammenfassung erstellt ({mode.SelectedItem}).");
        };
        clipboard.Click += (_, _) =>
        {
            if (Clipboard.ContainsText()) source.Text = Clipboard.GetText();
        };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(result.Text)) Clipboard.SetText(result.Text);
        };
        openFile.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Text und Logs|*.txt;*.log;*.md;*.csv|Alle Dateien|*.*" };
            if (dialog.ShowDialog() == DialogResult.OK) source.Text = File.ReadAllText(dialog.FileName);
        };

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        bar.Controls.AddRange([mode, summarize, clipboard, openFile, copy]);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 250 };
        split.Panel1.Controls.Add(source);
        split.Panel2.Controls.Add(result);
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(split);
        panel.Controls.Add(bar);
        return panel;
    }
}

internal sealed class TaskDocumentationModule : IToolboxModule
{
    public string Id => "task-documentation";
    public string Title => "Aufgabendokumentation";
    public string Category => "Dokumentation";
    public string Description => "Dokumentiert Problem, Analyse, Maßnahmen, Ergebnis, Bearbeiter und Zeitaufwand.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["dokumentation", "ticket", "bericht", "aufgabe", "maßnahmen", "export"];

    public Control CreateView()
    {
        var ticket = Field("Ticket / Vorgang");
        var customer = Field("Kunde / System");
        var technician = Field("Bearbeiter", Environment.UserName);
        var problem = Area("Problem / Auftrag");
        var analysis = Area("Analyse");
        var actions = Area("Durchgeführte Maßnahmen");
        var result = Area("Ergebnis / Übergabe");
        var started = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Width = 160, Value = DateTime.Now };
        var ended = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Width = 160, Value = DateTime.Now };
        var includeLog = new CheckBox { Text = "Toolbox-Protokoll als Zeitachse übernehmen", Checked = true, AutoSize = true };
        var preview = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5f) };
        var generate = new Button { Text = "Dokumentation erzeugen", AutoSize = true };
        var export = new Button { Text = "Exportieren", AutoSize = true };
        var copy = new Button { Text = "Kopieren", AutoSize = true };

        string Build() => DocumentationBuilder.Build(ticket.Text, customer.Text, technician.Text, started.Value, ended.Value,
            problem.Text, analysis.Text, actions.Text, result.Text, includeLog.Checked ? AppLog.Entries : []);

        generate.Click += (_, _) =>
        {
            preview.Text = Build();
            AppLog.Write("Aufgabendokumentation erzeugt.");
        };
        copy.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(preview.Text)) preview.Text = Build();
            Clipboard.SetText(preview.Text);
        };
        export.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(preview.Text)) preview.Text = Build();
            using var dialog = new SaveFileDialog
            {
                Filter = "Markdown|*.md|Textdatei|*.txt",
                FileName = $"Dokumentation-{SafeName(ticket.Text)}-{DateTime.Now:yyyyMMdd-HHmm}.md"
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            File.WriteAllText(dialog.FileName, preview.Text, Encoding.UTF8);
            AppLog.Write($"Aufgabendokumentation exportiert: {dialog.FileName}");
        };

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9, AutoScroll = true, Padding = new Padding(4) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, "Ticket / Vorgang", ticket);
        AddRow(form, "Kunde / System", customer);
        AddRow(form, "Bearbeiter", technician);
        AddRow(form, "Beginn / Ende", Pair(started, ended));
        AddRow(form, "Problem / Auftrag", problem, 100);
        AddRow(form, "Analyse", analysis, 100);
        AddRow(form, "Maßnahmen", actions, 120);
        AddRow(form, "Ergebnis", result, 100);
        AddRow(form, "Optionen", includeLog);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        buttons.Controls.AddRange([generate, export, copy]);
        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        right.Controls.Add(preview);
        right.Controls.Add(buttons);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 520 };
        split.Panel1.Controls.Add(form);
        split.Panel2.Controls.Add(right);
        return split;
    }

    private static TextBox Field(string name, string value = "") => new() { Text = value, Dock = DockStyle.Top, AccessibleName = name };
    private static TextBox Area(string name) => new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = name };
    private static Control Pair(Control first, Control second)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, WrapContents = false };
        panel.Controls.Add(first); panel.Controls.Add(new Label { Text = "bis", AutoSize = true, Padding = new Padding(5, 7, 5, 0) }); panel.Controls.Add(second);
        return panel;
    }
    private static void AddRow(TableLayoutPanel table, string label, Control control, int height = 36)
    {
        var row = table.RowCount - 1;
        table.RowStyles.Insert(row, new RowStyle(SizeType.Absolute, height));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 7, 0, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
        table.RowCount++;
    }
    private static string SafeName(string value) => Regex.Replace(string.IsNullOrWhiteSpace(value) ? "Vorgang" : value, "[^A-Za-z0-9_-]", "-");
}

internal static class LocalSummarizer
{
    private static readonly string[] ActionWords = ["muss", "soll", "prüfen", "beheben", "installieren", "aktualisieren", "neustarten", "klären", "empfohlen", "maßnahme", "todo"];

    public static string Summarize(string text, string mode)
    {
        var sentences = Regex.Split(Regex.Replace(text, @"\s+", " ").Trim(), @"(?<=[.!?])\s+")
            .Where(s => s.Length >= 20).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (sentences.Count == 0) return text.Trim();
        var keywords = Regex.Matches(text.ToLowerInvariant(), @"\b[\p{L}\d-]{5,}\b")
            .Select(m => m.Value).Where(w => !StopWords.Contains(w)).GroupBy(w => w)
            .OrderByDescending(g => g.Count()).Take(20).Select(g => g.Key).ToHashSet();
        var ranked = sentences.Select((s, i) => new { Sentence = s, Index = i, Score = keywords.Count(k => s.Contains(k, StringComparison.OrdinalIgnoreCase)) + (i == 0 ? 2 : 0) })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Index).ToList();
        if (mode == "Maßnahmenliste")
        {
            var actions = sentences.Where(s => ActionWords.Any(a => s.Contains(a, StringComparison.OrdinalIgnoreCase))).Take(10).ToList();
            if (actions.Count == 0) actions = ranked.Take(Math.Min(5, ranked.Count)).Select(x => x.Sentence).ToList();
            return string.Join(Environment.NewLine, actions.Select(a => "☐ " + a.Trim()));
        }
        var take = mode == "Stichpunkte" ? Math.Min(8, ranked.Count) : Math.Min(5, ranked.Count);
        var selected = ranked.Take(take).OrderBy(x => x.Index).Select(x => x.Sentence.Trim()).ToList();
        return mode == "Stichpunkte" ? string.Join(Environment.NewLine, selected.Select(s => "• " + s)) : string.Join(" ", selected);
    }

    private static readonly HashSet<string> StopWords = ["diese", "dieser", "dieses", "einen", "einer", "eines", "werden", "wurde", "wurden", "haben", "hatte", "nicht", "sowie", "durch", "unter", "über", "auch", "oder", "aber", "dass", "damit", "kann", "sind", "ist", "eine", "the", "and", "with", "from"];
}

internal static class DocumentationBuilder
{
    public static string Build(string ticket, string customer, string technician, DateTime started, DateTime ended,
        string problem, string analysis, string actions, string result, IReadOnlyList<string> log)
    {
        var duration = ended >= started ? ended - started : TimeSpan.Zero;
        var sb = new StringBuilder();
        sb.AppendLine($"# Aufgabendokumentation{(string.IsNullOrWhiteSpace(ticket) ? "" : $" – {ticket}")}").AppendLine();
        sb.AppendLine($"- **Kunde/System:** {Value(customer)}");
        sb.AppendLine($"- **Bearbeiter:** {Value(technician)}");
        sb.AppendLine($"- **Beginn:** {started:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"- **Ende:** {ended:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"- **Dauer:** {duration.TotalHours:0}:{duration.Minutes:00} Std.").AppendLine();
        Section(sb, "Problem / Auftrag", problem);
        Section(sb, "Analyse", analysis);
        Section(sb, "Durchgeführte Maßnahmen", actions);
        Section(sb, "Ergebnis / Übergabe", result);
        if (log.Count > 0)
        {
            sb.AppendLine("## Toolbox-Zeitachse").AppendLine();
            foreach (var line in log) sb.AppendLine($"- {line}");
            sb.AppendLine();
        }
        sb.AppendLine("---").AppendLine($"Erstellt mit NTB Toolbox am {DateTime.Now:dd.MM.yyyy HH:mm}.");
        return sb.ToString();
    }
    private static void Section(StringBuilder sb, string title, string value) => sb.AppendLine($"## {title}").AppendLine().AppendLine(Value(value)).AppendLine();
    private static string Value(string value) => string.IsNullOrWhiteSpace(value) ? "Nicht angegeben" : value.Trim();
}
