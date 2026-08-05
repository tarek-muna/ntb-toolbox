using NTB.Toolbox.Models;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal static class BuiltInModules
{
    public static IReadOnlyList<IToolboxModule> Create() =>
    [
        new TextModule("system-info", "Systeminformationen", "System", "Übersicht über Windows, Hardware, Laufwerke und Laufzeit.", ["computer", "hardware", "windows", "laufwerke"], () => Task.FromResult(SystemInfoService.CreateReport())),
        new TextModule("network-diagnostics", "Netzwerkdiagnose", "Netzwerk", "IP-Konfiguration, DNS und Erreichbarkeit prüfen.", ["ipconfig", "dns", "ping", "gateway"], NetworkDiagnosticsService.RunAsync),
        new TextModule("winget-list", "Winget Updates", "Software", "Verfügbare Paketaktualisierungen mit Winget anzeigen.", ["apps", "pakete", "updates", "winget"], async () => Format(await WingetService.ListUpgradesAsync())),
        new TextModule("winget-upgrade", "Alle Apps aktualisieren", "Software", "Alle verfügbaren Winget-Pakete aktualisieren.", ["apps", "upgrade", "winget"], async () => Format(await WingetService.UpgradeAllAsync())),
        new TextModule("sfc", "Systemdateien prüfen", "Windows-Reparatur", "Windows-Systemdateien mit SFC prüfen und reparieren.", ["sfc", "reparatur", "systemdateien"], async () => Format(await CommandRunner.RunAsync("sfc.exe", "/scannow"))),
        new TextModule("dism", "Windows-Abbild prüfen", "Windows-Reparatur", "Windows-Komponentenspeicher mit DISM analysieren.", ["dism", "scanhealth", "reparatur"], async () => Format(await CommandRunner.RunAsync("dism.exe", "/Online /Cleanup-Image /ScanHealth"))),
        new ActionModule("temp", "Temp-Ordner öffnen", "Dateien", "Temporären Benutzerordner im Explorer öffnen.", ["temp", "cache", "dateien"], () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", Path.GetTempPath()) { UseShellExecute = true }))
    ];

    private static string Format(CommandResult result) => $"ExitCode: {result.ExitCode}\r\n\r\n{result.StandardOutput}\r\n{result.StandardError}";
}

internal sealed class TextModule : IToolboxModule
{
    private readonly Func<Task<string>> _run;
    public TextModule(string id, string title, string category, string description, IReadOnlyCollection<string> keywords, Func<Task<string>> run)
    {
        Id = id; Title = title; Category = category; Description = description; Keywords = keywords; _run = run;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public IReadOnlyCollection<string> Keywords { get; }

    public Control CreateView()
    {
        var output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10), Text = Description };
        var run = new Button { Text = "Ausführen", Dock = DockStyle.Top, Height = 38 };
        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            output.Text = "Wird ausgeführt …";
            try { output.Text = await _run(); }
            catch (Exception ex) { output.Text = "Fehler: " + ex.Message; }
            finally { run.Enabled = true; }
        };
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(run);
        return panel;
    }
}

internal sealed class ActionModule : IToolboxModule
{
    private readonly Action _action;
    public ActionModule(string id, string title, string category, string description, IReadOnlyCollection<string> keywords, Action action)
    {
        Id = id; Title = title; Category = category; Description = description; Keywords = keywords; _action = action;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public IReadOnlyCollection<string> Keywords { get; }

    public Control CreateView()
    {
        var button = new Button { Text = Title, AutoSize = true, Height = 38 };
        button.Click += (_, _) => _action();
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = Description, AutoSize = true, MaximumSize = new Size(650, 0) });
        panel.Controls.Add(button);
        return panel;
    }
}
