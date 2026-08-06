using System.Net.NetworkInformation;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class DashboardModule : IToolboxModule
{
    public string Id => "dashboard";
    public string Title => "Übersicht";
    public string Category => "Start";
    public string Description => "Systemstatus, Schnellzugriffe und letzte Toolbox-Aktivitäten.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["start", "dashboard", "status", "übersicht"];

    public Control CreateView()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 3
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(Card("Version", "NTB Toolbox 1.0.1", "Stabilitäts- und UX-Update"), 0, 0);
        root.Controls.Add(Card("Netzwerk", NetworkInterface.GetIsNetworkAvailable() ? "Verbindung verfügbar" : "Keine Verbindung erkannt", "Lokaler Windows-Netzwerkstatus"), 1, 0);
        root.Controls.Add(Card("Benutzer", Environment.UserName, Environment.MachineName), 0, 1);
        root.Controls.Add(Card("Protokoll", $"{AppLog.Entries.Count} Einträge", "Details über 'Protokoll anzeigen'"), 1, 1);

        var recent = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            Text = BuildRecentLog()
        };
        var group = new GroupBox { Text = "Letzte Aktivitäten", Dock = DockStyle.Fill, Padding = new Padding(10) };
        group.Controls.Add(recent);
        root.Controls.Add(group, 0, 2);
        root.SetColumnSpan(group, 2);
        return root;
    }

    private static Control Card(string heading, string value, string details)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), Margin = new Padding(6), BorderStyle = BorderStyle.FixedSingle };
        panel.Controls.Add(new Label { Text = details, Dock = DockStyle.Bottom, Height = 28, ForeColor = Color.DimGray });
        panel.Controls.Add(new Label { Text = value, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft });
        panel.Controls.Add(new Label { Text = heading.ToUpperInvariant(), Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Color.SteelBlue });
        return panel;
    }

    private static string BuildRecentLog()
    {
        var entries = AppLog.Entries.TakeLast(12).ToArray();
        return entries.Length == 0
            ? "Noch keine Aktivitäten in dieser Sitzung."
            : string.Join(Environment.NewLine, entries);
    }
}
