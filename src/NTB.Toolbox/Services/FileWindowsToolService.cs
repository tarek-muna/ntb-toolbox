using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NTB.Toolbox.Services;

internal static class FileWindowsToolService
{
    private const uint RecycleNoConfirmation = 0x00000001;
    private const uint RecycleNoProgressUi = 0x00000002;
    private const uint RecycleNoSound = 0x00000004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    public static async Task<string> RestartExplorerAsync()
    {
        var processes = Process.GetProcessesByName("explorer");
        foreach (var process in processes)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            finally
            {
                process.Dispose();
            }
        }

        await Task.Delay(500);
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        return $"Windows Explorer wurde neu gestartet. Beendete Prozesse: {processes.Length}";
    }

    public static string EmptyRecycleBin()
    {
        var result = SHEmptyRecycleBin(IntPtr.Zero, null, RecycleNoConfirmation | RecycleNoProgressUi | RecycleNoSound);
        return result == 0
            ? "Der Papierkorb wurde geleert."
            : $"Der Papierkorb konnte nicht vollständig geleert werden. Fehlercode: 0x{result:X8}";
    }

    public static Task<string> CleanUserTempAsync()
    {
        return Task.Run(() =>
        {
            var root = Path.GetTempPath();
            long freedBytes = 0;
            var deletedFiles = 0;
            var deletedDirectories = 0;
            var skipped = 0;

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var info = new FileInfo(file);
                    var length = info.Exists ? info.Length : 0;
                    info.Delete();
                    freedBytes += length;
                    deletedFiles++;
                }
                catch
                {
                    skipped++;
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var size = CalculateDirectorySize(directory);
                    Directory.Delete(directory, recursive: true);
                    freedBytes += size;
                    deletedDirectories++;
                }
                catch
                {
                    skipped++;
                }
            }

            return $"Temp-Bereinigung abgeschlossen.\r\n\r\n" +
                   $"Gelöschte Dateien: {deletedFiles}\r\n" +
                   $"Gelöschte Ordner: {deletedDirectories}\r\n" +
                   $"Übersprungen/gesperrt: {skipped}\r\n" +
                   $"Freigegeben: {FormatBytes(freedBytes)}\r\n" +
                   $"Pfad: {root}";
        });
    }

    public static async Task<string> CalculateHashesAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Die ausgewählte Datei wurde nicht gefunden.", filePath);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        var sha256 = await SHA256.HashDataAsync(stream);
        stream.Position = 0;
        var sha512 = await SHA512.HashDataAsync(stream);

        var info = new FileInfo(filePath);
        var output = new StringBuilder();
        output.AppendLine($"Datei: {info.FullName}");
        output.AppendLine($"Größe: {FormatBytes(info.Length)}");
        output.AppendLine();
        output.AppendLine($"SHA-256: {Convert.ToHexString(sha256)}");
        output.AppendLine();
        output.AppendLine($"SHA-512: {Convert.ToHexString(sha512)}");
        return output.ToString();
    }

    private static long CalculateDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }
        return size;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
