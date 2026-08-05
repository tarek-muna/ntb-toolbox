using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class TicketGeneratorModule : IToolboxModule
{
    public string Id => "ticket-generator";
    public string Title => "Ticket-Generator";
    public string Category => "Dokumentation";
    public string Description => "Erstellt strukturierte Tickettexte für Zammad, Jira, GLPI und allgemeine Supportsysteme.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["ticket", "zammad", "jira", "glpi", "support", "incident", "service request"];

    public Control CreateView()
    {
        var system = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        system.Items.AddRange(["Allgemein", "Zammad", "Jira", "GLPI"]);
        system.SelectedIndex = 0;

        var type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
        type.Items.AddRange(["Störung", "Serviceanfrage", "Change", "Problem"]);
        type.SelectedIndex = 0;

        var priority = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        priority.Items.AddRange(["Niedrig", "Normal", "Hoch", "Kritisch"]);
        priority.SelectedIndex = 1;

        var subject = Field("Betreff");
        var customer = Field("Kunde / System");
        var requester = Field("Anforderer");
        var problem = Area("Problem / Anfrage", 90);
        var analysis = Area("Analyse", 100);
        var actions = Area("Durchgeführte Maßnahmen", 110);
        var result = Area("Ergebnis / Nächste Schritte", 90);
        var tags = Field("Tags, kommagetrennt");
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };

        var generate = new Button { Text = "Tickettext erzeugen", AutoSize = true };
        var copy = new Button { Text = "Kopieren", AutoSize = true };
        var save = new Button { Text = "Exportieren", AutoSize = true };
        var clear = new Button { Text = "Leeren", AutoSize = true };

        generate.Click += (_, _) =>
        {
            output.Text = Build(
                system.Text, type.Text, priority.Text, subject.Text, customer.Text, requester.Text,
                problem.Text, analysis.Text, actions.Text, result.Text, tags.Text);
            AppLog.Write($"Tickettext für {system.Text} erzeugt.");
        };

        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(output.Text)) Clipboard.SetText(output.Text);
        };

        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(output.Text)) return;
            using var dialog = new SaveFileDialog
            {
                Filter = "Textdatei (*.txt)|*.txt|Markdown (*.md)|*.md",
                FileName = $"ticket-{DateTime.Now:yyyyMMdd-HHmm}.txt"
            };
            if (dialog.ShowDialog() == DialogResult.OK) File.WriteAllText(dialog.FileName, output.Text);
        };

        clear.Click += (_, _) =>
        {
            foreach (var box in new[] { subject, customer, requester, problem, analysis, actions, result, tags }) box.Clear();
            output.Clear();
        };

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, AutoSize = false };
        options.Controls.AddRange([new Label { Text = "System", AutoSize = true, Padding = new Padding(0, 8, 0, 0) }, system,
            new Label { Text = "Typ", AutoSize = true, Padding = new Padding(12, 8, 0, 0) }, type,
            new Label { Text = "Priorität", AutoSize = true, Padding = new Padding(12, 8, 0, 0) }, priority]);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        buttons.Controls.AddRange([generate, copy, save, clear]);

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 8, 0, 8) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(form, "Betreff", subject);
        AddRow(form, "Kunde / System", customer);
        AddRow(form, "Anforderer", requester);
        AddRow(form, "Problem / Anfrage", problem);
        AddRow(form, "Analyse", analysis);
        AddRow(form, "Maßnahmen", actions);
        AddRow(form, "Ergebnis / Nächste Schritte", result);
        AddRow(form, "Tags", tags);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(buttons);
        panel.Controls.Add(form);
        panel.Controls.Add(options);
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

    private static string Build(string system, string type, string priority, string subject, string customer, string requester, string problem, string analysis, string actions, string result, string tags)
    {
        var header = system switch
        {
            "Jira" => $"h2. {subject}\r\n*Typ:* {type}\r\n*Priorität:* {priority}",
            "GLPI" => $"Titel: {subject}\r\nKategorie: {type}\r\nDringlichkeit: {priority}",
            "Zammad" => $"Betreff: {subject}\r\nTyp: {type}\r\nPriorität: {priority}",
            _ => $"{subject}\r\nTyp: {type} | Priorität: {priority}"
        };

        return $"{header}\r\nKunde/System: {customer}\r\nAnforderer: {requester}\r\nTags: {tags}\r\n\r\nPROBLEM / ANFRAGE\r\n{problem}\r\n\r\nANALYSE\r\n{analysis}\r\n\r\nDURCHGEFÜHRTE MASSNAHMEN\r\n{actions}\r\n\r\nERGEBNIS / NÄCHSTE SCHRITTE\r\n{result}";
    }
}
