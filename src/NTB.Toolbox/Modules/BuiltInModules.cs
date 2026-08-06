using NTB.Toolbox.Models;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal static class BuiltInModules
{
    public static IReadOnlyList<IToolboxModule> Create() =>
    [
        new DashboardModule(),
        new AiSettingsModule(),
        new FisiHandbookModule(),
        new SummaryModule(),
        new TaskDocumentationModule(),
        new DocumentationWorkflowModule(),
        new KnowledgeBaseModule(),
        new TicketGeneratorModule(),
        new ActiveDirectoryModule(),
        new TextModule("system-info", "Systeminformationen", "System", "Übersicht über Windows, Hardware, Laufwerke und Laufzeit.", ["computer", "hardware", "windows", "laufwerke"], false, () => Task.FromResult(SystemInfoService.CreateReport())),
        new TextModule("network-diagnostics", "Netzwerkdiagnose", "Netzwerk", "IP-Konfiguration, DNS und Erreichbarkeit prüfen.", ["ipconfig", "dns", "ping", "gateway"], false, NetworkDiagnosticsService.RunAsync),
        new OsiAnalysisModule(),
        new QuickAiModule(),
        new NetworkInputModule("ping", "Ping", "Sendet mehrere ICMP-Anfragen und berechnet Laufzeitstatistiken.", ["icmp", "latenz", "erreichbarkeit"], "Host oder IP-Adresse", host => NetworkToolService.PingAsync(host)),
        new NetworkInputModule("traceroute", "Traceroute", "Ermittelt die Netzwerkstationen bis zum Zielsystem.", ["route", "hops", "ttl", "tracert"], "Host oder IP-Adresse", NetworkToolService.TraceRouteAsync),
        new NetworkInputModule("dns-lookup", "DNS-Lookup", "Löst Hostnamen und IP-Adressen über DNS auf.", ["dns", "hostname", "adresse", "auflösung"], "Hostname oder IP-Adresse", NetworkToolService.DnsLookupAsync),
        new TcpPortModule(),
        new PortScanModule(),
        new WakeOnLanModule(),
        new WlanProfilesModule(),
        new TextModule("winget-list", "Winget Updates", "Software", "Verfügbare Paketaktualisierungen mit Winget anzeigen.", ["apps", "pakete", "updates", "winget"], false, async () => Format(await WingetService.ListUpgradesAsync())),
        new TextModule("winget-upgrade", "Alle Apps aktualisieren", "Software", "Alle verfügbaren Winget-Pakete aktualisieren.", ["apps", "upgrade", "winget"], true, async () => Format(await WingetService.UpgradeAllAsync())),
        new TextModule("sfc", "Systemdateien prüfen", "Windows-Reparatur", "Windows-Systemdateien mit SFC prüfen und reparieren.", ["sfc", "reparatur", "systemdateien"], true, async () => Format(await CommandRunner.RunAsync("sfc.exe", "/scannow"))),
        new TextModule("dism", "Windows-Abbild prüfen", "Windows-Reparatur", "Windows-Komponentenspeicher mit DISM analysieren.", ["dism", "scanhealth", "reparatur"], true, async () => Format(await CommandRunner.RunAsync("dism.exe", "/Online /Cleanup-Image /ScanHealth"))),
        new ConfirmedTaskModule("restart-explorer", "Explorer neu starten", "Windows", "Beendet den Windows Explorer und startet ihn anschließend neu.", ["explorer", "shell", "taskleiste", "desktop"], false, "Explorer neu starten", "Offene Explorer-Fenster werden geschlossen. Fortfahren?", FileWindowsToolService.RestartExplorerAsync),
        new ConfirmedTaskModule("empty-recycle-bin", "Papierkorb leeren", "Dateien", "Leert den Windows-Papierkorb für alle Laufwerke.", ["papierkorb", "recycle", "löschen"], false, "Papierkorb leeren", "Die Dateien im Papierkorb werden dauerhaft gelöscht. Fortfahren?", () => Task.FromResult(FileWindowsToolService.EmptyRecycleBin())),
        new ConfirmedTaskModule("clean-user-temp", "Temp-Dateien bereinigen", "Dateien", "Löscht nicht verwendete Dateien und Ordner im Benutzer-Temp-Verzeichnis.", ["temp", "cache", "bereinigung", "speicherplatz"], false, "Temp-Dateien bereinigen", "Nicht gesperrte Dateien im Benutzer-Temp-Verzeichnis werden gelöscht. Fortfahren?", FileWindowsToolService.CleanUserTempAsync),
        new FileHashModule(),
        new ActionModule("temp", "Temp-Ordner öffnen", "Dateien", "Temporären Benutzerordner im Explorer öffnen.", ["temp", "cache", "dateien"], false, () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", Path.GetTempPath()) { UseShellExecute = true }))
    ];

    private static string Format(CommandResult result) => $"ExitCode: {result.ExitCode}\r\n\r\n{result.StandardOutput}\r\n{result.StandardError}";
}

internal sealed class TextModule : IToolboxModule
{
    private readonly Func<Task<string>> _run;

    public TextModule(string id, string title, string category, string description, IReadOnlyCollection<string> keywords, bool requiresAdministrator, Func<Task<string>> run)
    {
        Id = id;
        Title = title;
        Category = category;
        Description = description;
        Keywords = keywords;
        RequiresAdministrator = requiresAdministrator;
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
        var status = new Label { Text = "Bereit", Dock = DockStyle.Top, Height = 24, Padding = new Padding(4, 4, 0, 0) };
        var progress = new ProgressBar { Dock = DockStyle.Top, Height = 6, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
        var run = new Button { Text = "Ausführen", Dock = DockStyle.Top, Height = 38 };
        run.Click += async (_, _) =>
        {
            run.Enabled = false;
            progress.Visible = true;
            status.Text = "Wird ausgeführt …";
            output.Text = "Bitte warten …";
            AppLog.Write($"{Title} gestartet.");
            try
            {
                output.Text = await _run();
                status.Text = "Erfolgreich abgeschlossen";
                AppLog.Write($"{Title} abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                status.Text = "Fehlgeschlagen";
                AppErrorHandler.Handle(ex, Title);
            }
            finally
            {
                progress.Visible = false;
                run.Enabled = true;
            }
        };
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(status);
        panel.Controls.Add(progress);
        panel.Controls.Add(run);
        return panel;
    }
}

internal sealed class ActionModule : IToolboxModule
{
    private readonly Action _action;

    public ActionModule(string id, string title, string category, string description, IReadOnlyCollection<string> keywords, bool requiresAdministrator, Action action)
    {
        Id = id;
        Title = title;
        Category = category;
        Description = description;
        Keywords = keywords;
        RequiresAdministrator = requiresAdministrator;
        _action = action;
    }

    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string Description { get; }
    public bool RequiresAdministrator { get; }
    public IReadOnlyCollection<string> Keywords { get; }

    public Control CreateView()
    {
        var button = new Button { Text = Title, AutoSize = true, Height = 38 };
        button.Click += (_, _) =>
        {
            try
            {
                AppLog.Write($"{Title} geöffnet.");
                _action();
            }
            catch (Exception ex)
            {
                AppErrorHandler.Handle(ex, Title);
            }
        };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label { Text = Description, AutoSize = true, MaximumSize = new Size(650, 0) });
        panel.Controls.Add(button);
        return panel;
    }
}
