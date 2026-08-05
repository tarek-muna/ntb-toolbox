using System.Text.Json;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class DocumentationWorkflowModule : IToolboxModule
{
    public string Id => "documentation-workflow";
    public string Title => "Dokumentations-Workflow";
    public string Category => "Dokumentation";
    public string Description => "Erfasst einen Supporteinsatz einmal und erzeugt daraus Dokumentation, Tickettext oder einen lokalen Wissenseintrag.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["workflow", "ticket", "wissensdatenbank", "dokumentation", "bericht", "einsatz"];

    public Control CreateView()
    {
        var ticket = Field("Ticket / Vorgang");
        var customer = Field("Kunde / System");
        var subject = Field("Betreff");
        var problem = Area("Problem / Auftrag", 85);
        var analysis = Area("Analyse", 95);
        var actions = Area("Durchgeführte Maßnahmen", 105);
        var result = Area("Ergebnis / Nächste Schritte", 85);
        var tags = Field("Tags, kommagetrennt");
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };

        var documentation = new Button { Text = "Dokumentation", AutoSize = true };
        var ticketText = new Button { Text = "Tickettext", AutoSize = true };
        var saveKnowledge = new Button { Text = "In Wissen speichern", AutoSize = true };
        var copy = new Button { Text = "Kopieren", AutoSize = true };
        var export = new Button { Text = "Exportieren", AutoSize = true };

        documentation.Click += (_, _) =>
        {
            output.Text = BuildDocumentation(ticket.Text, customer.Text, subject.Text, problem.Text, analysis.Text, actions.Text, result.Text);
            AppLog.Write("Technische Dokumentation erzeugt.");
        };
        ticketText.Click += (_, _) =>
        {
            output.Text = BuildTicket(ticket.Text, customer.Text, subject.Text, problem.Text, analysis.Text, actions.Text, result.Text, tags.Text);
            AppLog.Write("Tickettext aus Dokumentations-Workflow erzeugt.");
        };
        saveKnowledge.Click += (_, _) =>
        {
            var content = BuildDocumentation(ticket.Text, customer.Text, subject.Text, problem.Text, analysis.Text, actions.Text, result.Text);
            SaveKnowledge(subject.Text, tags.Text, content);
            output.Text = content;
            MessageBox.Show("Der Eintrag wurde lokal in der Workflow-Wissensablage gespeichert.", "Gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AppLog.Write("Dokumentation in lokaler Wissensablage gespeichert.");
        };
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(output.Text)) Clipboard.SetText(output.Text); };
        export.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(output.Text)) return;
            using var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md|Textdatei (*.txt)|*.txt", FileName = $"dokumentation-{DateTime.Now:yyyyMMdd-HHmm}.md" };
            if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllText(dialog.FileName, output.Text);
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        buttons.Controls.AddRange([documentation, ticketText, saveKnowledge, copy, export]);

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 8, 0, 8) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, "Ticket / Vorgang", ticket);
        AddRow(form, "Kunde / System", customer);
        AddRow(form, "Betreff", subject);
        AddRow(form, "Problem / Auftrag", problem);
        AddRow(form, "Analyse", analysis);
        AddRow(form, "Maßnahmen", actions);
        AddRow(form, "Ergebnis", result);
        AddRow(form, "Tags", tags);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(buttons);
        panel.Controls.Add(form);
        return panel;
    }

    private static TextBox Field(string placeholder) => new() { Dock = DockStyle.Fill, PlaceholderText = placeholder };
    private static TextBox Area(string placeholder, int height) => new() { Dock = DockStyle.Fill, Multiline = true, Height = height, ScrollBars = ScrollBars.Vertical, PlaceholderText = placeholder };

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 8, 8, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private static string BuildDocumentation(string ticket, string customer, string subject, string problem, string analysis, string actions, string result) =>
        $"# {subject}\r\n\r\n**Ticket:** {ticket}\r\n**Kunde/System:** {customer}\r\n**Erstellt:** {DateTime.Now:dd.MM.yyyy HH:mm}\r\n\r\n## Problem / Auftrag\r\n{problem}\r\n\r\n## Analyse\r\n{analysis}\r\n\r\n## Durchgeführte Maßnahmen\r\n{actions}\r\n\r\n## Ergebnis / Nächste Schritte\r\n{result}";

    private static string BuildTicket(string ticket, string customer, string subject, string problem, string analysis, string actions, string result, string tags) =>
        $"Betreff: {subject}\r\nTicket: {ticket}\r\nKunde/System: {customer}\r\nTags: {tags}\r\n\r\nPROBLEM / ANFRAGE\r\n{problem}\r\n\r\nANALYSE\r\n{analysis}\r\n\r\nMASSNAHMEN\r\n{actions}\r\n\r\nERGEBNIS / NÄCHSTE SCHRITTE\r\n{result}";

    private static void SaveKnowledge(string title, string tags, string content)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NTB Toolbox");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "workflow-knowledge.json");
        List<WorkflowKnowledgeEntry> entries;
        try
        {
            entries = File.Exists(path)
                ? JsonSerializer.Deserialize<List<WorkflowKnowledgeEntry>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch
        {
            entries = [];
        }
        entries.Add(new WorkflowKnowledgeEntry(Guid.NewGuid(), string.IsNullOrWhiteSpace(title) ? "Dokumentation" : title, tags, content, DateTime.Now));
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record WorkflowKnowledgeEntry(Guid Id, string Title, string Tags, string Content, DateTime CreatedAt);
}
