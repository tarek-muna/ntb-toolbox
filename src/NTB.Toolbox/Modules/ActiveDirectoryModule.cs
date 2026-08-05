using System.Text;
using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class ActiveDirectoryModule : IToolboxModule
{
    public string Id => "active-directory";
    public string Title => "Active Directory";
    public string Category => "Enterprise";
    public string Description => "Domänenübersicht, Objekt-Suche und AD-Diagnose im schreibgeschützten Modus.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["active directory", "ad", "domain", "benutzer", "gruppen", "computer", "fsmo", "dcdiag", "repadmin"];

    public Control CreateView()
    {
        var query = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Name, Anmeldename oder Teilbegriff" };
        var type = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        type.Items.AddRange(["Benutzer", "Gruppen", "Computer"]);
        type.SelectedIndex = 0;

        var search = new Button { Text = "Suchen", AutoSize = true };
        var overview = new Button { Text = "Domänenübersicht", AutoSize = true };
        var diagnostics = new Button { Text = "DC / Replikation", AutoSize = true };
        var copy = new Button { Text = "Kopieren", AutoSize = true };
        var output = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            Text = "Dieses Modul führt ausschließlich lesende Active-Directory-Abfragen aus.\r\nBenötigt: Domänenzugriff und das Windows-Modul ActiveDirectory (RSAT)."
        };

        async Task RunAsync(Func<Task<string>> action, string logText)
        {
            search.Enabled = overview.Enabled = diagnostics.Enabled = false;
            output.Text = "Abfrage läuft …";
            AppLog.Write(logText + " gestartet.");
            try
            {
                output.Text = await action();
                AppLog.Write(logText + " abgeschlossen.");
            }
            catch (Exception ex)
            {
                output.Text = "Fehler: " + ex.Message;
                AppLog.Write(logText + " fehlgeschlagen: " + ex.Message);
            }
            finally
            {
                search.Enabled = overview.Enabled = diagnostics.Enabled = true;
            }
        }

        search.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(query.Text))
            {
                MessageBox.Show("Bitte einen Suchbegriff eingeben.", "Active Directory", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await RunAsync(() => ActiveDirectoryService.SearchAsync(type.Text, query.Text), $"AD-Suche {type.Text}");
        };
        overview.Click += async (_, _) => await RunAsync(ActiveDirectoryService.DomainOverviewAsync, "AD-Domänenübersicht");
        diagnostics.Click += async (_, _) => await RunAsync(ActiveDirectoryService.DiagnosticsAsync, "AD-Diagnose");
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(output.Text)) Clipboard.SetText(output.Text); };
        query.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !string.IsNullOrWhiteSpace(query.Text))
            {
                e.SuppressKeyPress = true;
                await RunAsync(() => ActiveDirectoryService.SearchAsync(type.Text, query.Text), $"AD-Suche {type.Text}");
            }
        };

        var bar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 42, ColumnCount = 6, Padding = new Padding(0, 0, 0, 4) };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 2; i < 6; i++) bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.Controls.Add(type, 0, 0);
        bar.Controls.Add(query, 1, 0);
        bar.Controls.Add(search, 2, 0);
        bar.Controls.Add(overview, 3, 0);
        bar.Controls.Add(diagnostics, 4, 0);
        bar.Controls.Add(copy, 5, 0);

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(output);
        panel.Controls.Add(bar);
        return panel;
    }
}

internal static class ActiveDirectoryService
{
    public static Task<string> SearchAsync(string type, string query)
    {
        var safe = PsLiteral(query);
        var command = type switch
        {
            "Gruppen" => $"Get-ADGroup -Filter \"Name -like '*{safe}*' -or SamAccountName -like '*{safe}*'\" -Properties Description,GroupScope,GroupCategory | Select-Object Name,SamAccountName,GroupScope,GroupCategory,Description,DistinguishedName | Format-List",
            "Computer" => $"Get-ADComputer -Filter \"Name -like '*{safe}*' -or DNSHostName -like '*{safe}*'\" -Properties DNSHostName,OperatingSystem,OperatingSystemVersion,Enabled,LastLogonDate | Select-Object Name,DNSHostName,OperatingSystem,OperatingSystemVersion,Enabled,LastLogonDate,DistinguishedName | Format-List",
            _ => $"Get-ADUser -Filter \"Name -like '*{safe}*' -or SamAccountName -like '*{safe}*' -or UserPrincipalName -like '*{safe}*'\" -Properties DisplayName,Mail,Enabled,LockedOut,LastLogonDate,PasswordLastSet,Department,Title | Select-Object Name,SamAccountName,UserPrincipalName,DisplayName,Mail,Enabled,LockedOut,LastLogonDate,PasswordLastSet,Department,Title,DistinguishedName | Format-List"
        };
        return RunAdAsync(command);
    }

    public static Task<string> DomainOverviewAsync() => RunAdAsync("""
$domain = Get-ADDomain
$forest = Get-ADForest
$dcs = Get-ADDomainController -Filter * | Sort-Object HostName
'=== DOMÄNE ==='
$domain | Select-Object DNSRoot,NetBIOSName,DomainMode,PDCEmulator,RIDMaster,InfrastructureMaster,DistinguishedName | Format-List
'=== FOREST ==='
$forest | Select-Object Name,ForestMode,SchemaMaster,DomainNamingMaster,Domains,GlobalCatalogs | Format-List
'=== DOMÄNENCONTROLLER ==='
$dcs | Select-Object HostName,IPv4Address,Site,OperatingSystem,IsGlobalCatalog,OperationMasterRoles | Format-Table -AutoSize
""");

    public static async Task<string> DiagnosticsAsync()
    {
        var ad = await RunAdAsync("Get-ADDomainController -Filter * | Select-Object HostName,Site,IPv4Address,IsGlobalCatalog,OperationMasterRoles | Format-Table -AutoSize");
        var rep = await RunProcessAsync("repadmin.exe", "/replsummary");
        var dc = await RunProcessAsync("dcdiag.exe", "/test:Advertising /test:Services /test:SysVolCheck /test:NetLogons /q");
        return $"=== DOMÄNENCONTROLLER ===\r\n{ad}\r\n\r\n=== REPLIKATIONSÜBERSICHT ===\r\n{rep}\r\n\r\n=== DCDIAG KURZTEST ===\r\n{dc}";
    }

    private static async Task<string> RunAdAsync(string command)
    {
        var script = $"$ErrorActionPreference='Stop'; if (-not (Get-Module -ListAvailable ActiveDirectory)) {{ throw 'Das ActiveDirectory-PowerShell-Modul fehlt. Installieren Sie RSAT: Active Directory Domain Services and Lightweight Directory Services Tools.' }}; Import-Module ActiveDirectory; {command} | Out-String -Width 240";
        var result = await CommandRunner.RunAsync("powershell.exe", $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{EscapeArgument(script)}\"");
        if (result.ExitCode != 0) throw new InvalidOperationException(Explain(result.StandardError));
        return string.IsNullOrWhiteSpace(result.StandardOutput) ? "Keine passenden Ergebnisse gefunden." : result.StandardOutput.Trim();
    }

    private static async Task<string> RunProcessAsync(string file, string arguments)
    {
        try
        {
            var result = await CommandRunner.RunAsync(file, arguments);
            var text = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
            return string.IsNullOrWhiteSpace(text) ? "Keine Fehler oder Ausgaben gemeldet." : text;
        }
        catch (Exception ex)
        {
            return $"Werkzeug nicht verfügbar oder nicht ausführbar: {ex.Message}";
        }
    }

    private static string PsLiteral(string value) => value.Replace("'", "''").Replace("*", "`*").Replace("?", "`?");
    private static string EscapeArgument(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", "; ");

    private static string Explain(string error)
    {
        if (error.Contains("Unable to find a default server", StringComparison.OrdinalIgnoreCase) || error.Contains("server has returned", StringComparison.OrdinalIgnoreCase))
            return "Keine Active-Directory-Domäne erreichbar. Prüfen Sie Domänenmitgliedschaft, VPN, DNS und Netzwerkverbindung.\r\n\r\n" + error.Trim();
        if (error.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
            return "Die aktuellen Anmeldedaten besitzen nicht die erforderlichen Leserechte.\r\n\r\n" + error.Trim();
        return error.Trim();
    }
}
