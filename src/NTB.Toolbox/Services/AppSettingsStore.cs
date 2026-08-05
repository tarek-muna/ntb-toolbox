using System.Text;
using System.Text.Json;

namespace NTB.Toolbox.Services;

internal sealed class AppSettings
{
    public HashSet<string> FavoriteModuleIds { get; set; } = [];
    public AppTheme Theme { get; set; } = AppTheme.Light;
}

internal static class AppSettingsStore
{
    private static readonly object SettingsLock = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "NTB Toolbox");

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load()
    {
        lock (SettingsLock)
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                AppLog.Write($"Einstellungen konnten nicht geladen werden: {ex.Message}");
                TryBackup(FilePath, "corrupt");
                return new AppSettings();
            }
        }
    }

    public static void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (SettingsLock)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporaryPath = FilePath + ".tmp";
            var backupPath = FilePath + ".bak";
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporaryPath, json, Encoding.UTF8);

            try
            {
                if (File.Exists(FilePath))
                    File.Replace(temporaryPath, FilePath, backupPath, ignoreMetadataErrors: true);
                else
                    File.Move(temporaryPath, FilePath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }

    private static void TryBackup(string path, string suffix)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backupPath = $"{path}.{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backupPath, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Sicherung der Einstellungsdatei fehlgeschlagen: {ex.Message}");
        }
    }
}
