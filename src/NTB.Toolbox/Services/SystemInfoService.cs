using System.Runtime.InteropServices;

namespace NTB.Toolbox.Services;

internal static class SystemInfoService
{
    public static string CreateReport()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.IsReady)
            .Select(d => $"{d.Name} {Format(d.AvailableFreeSpace)} frei von {Format(d.TotalSize)} ({d.DriveFormat})");
        return $"Computer: {Environment.MachineName}\r\nBenutzer: {Environment.UserName}\r\nWindows: {RuntimeInformation.OSDescription}\r\nArchitektur: {RuntimeInformation.OSArchitecture}\r\nProzessoren: {Environment.ProcessorCount}\r\n.NET: {Environment.Version}\r\n\r\nLaufwerke:\r\n{string.Join("\r\n", drives)}";
    }

    private static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }
}
