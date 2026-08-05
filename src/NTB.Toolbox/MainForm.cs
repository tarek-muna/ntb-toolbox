using NTB.Toolbox.Services;

namespace NTB.Toolbox;

internal sealed class MainForm : Form
{
    private readonly TextBox _output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10) };

    public MainForm()
    {
        Text = "NTB Toolbox 0.1.0";
        Width = 1000;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8), WrapContents = false };
        buttons.Controls.Add(Button("Systeminformationen", () => Show(SystemInfoService.CreateReport())));
        buttons.Controls.Add(Button("Netzwerkdiagnose", async () => Show(await NetworkDiagnosticsService.RunAsync())));
        buttons.Controls.Add(Button("Winget Updates", async () => ShowResult(await WingetService.ListUpgradesAsync())));
        buttons.Controls.Add(Button("Alle Apps aktualisieren", async () => ShowResult(await WingetService.UpgradeAllAsync())));
        buttons.Controls.Add(Button("SFC starten", async () => ShowResult(await CommandRunner.RunAsync("sfc.exe", "/scannow"))));
        buttons.Controls.Add(Button("DISM prüfen", async () => ShowResult(await CommandRunner.RunAsync("dism.exe", "/Online /Cleanup-Image /ScanHealth"))));
        buttons.Controls.Add(Button("Temp öffnen", () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", Path.GetTempPath()) { UseShellExecute = true })));

        Controls.Add(_output);
        Controls.Add(buttons);
    }

    private static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32 };
        button.Click += (_, _) => action();
        return button;
    }

    private void Show(string text) => _output.Text = text;
    private void ShowResult(Models.CommandResult result) => Show($"ExitCode: {result.ExitCode}\r\n\r\n{result.StandardOutput}\r\n{result.StandardError}");
}
