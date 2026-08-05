namespace NTB.Toolbox.Services;

internal static class WingetService
{
    public static Task<Models.CommandResult> ListUpgradesAsync() => CommandRunner.RunAsync("winget.exe", "upgrade --accept-source-agreements");
    public static Task<Models.CommandResult> UpgradeAllAsync() => CommandRunner.RunAsync("winget.exe", "upgrade --all --accept-package-agreements --accept-source-agreements");
}
