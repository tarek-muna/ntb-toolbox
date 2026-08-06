using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NTB.Toolbox.Services;

internal enum AiProviderMode
{
    NtbProxy,
    OpenAi
}

internal sealed class AiConfiguration
{
    public bool Enabled { get; set; }
    public AiProviderMode Mode { get; set; } = AiProviderMode.NtbProxy;
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "gpt-5";
    public string SystemInstruction { get; set; } = "Du bist der kurze, präzise IT-Assistent der NTB Toolbox. Antworte auf Deutsch und nenne bei Unsicherheit klar die Grenzen.";
    public int TimeoutSeconds { get; set; } = 60;
    public string Secret { get; set; } = "";
}

internal static class AiConfigurationService
{
    private static readonly object Sync = new();
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NTB Toolbox");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "ai-settings.json");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NTB.Toolbox.AI.Settings.v1");

    public static AiConfiguration Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(FilePath)) return LoadEnvironmentFallback();
                var stored = JsonSerializer.Deserialize<StoredAiConfiguration>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (stored is null) return LoadEnvironmentFallback();
                return new AiConfiguration
                {
                    Enabled = stored.Enabled,
                    Mode = stored.Mode,
                    Endpoint = stored.Endpoint ?? "",
                    Model = string.IsNullOrWhiteSpace(stored.Model) ? "gpt-5" : stored.Model,
                    SystemInstruction = string.IsNullOrWhiteSpace(stored.SystemInstruction) ? new AiConfiguration().SystemInstruction : stored.SystemInstruction,
                    TimeoutSeconds = Math.Clamp(stored.TimeoutSeconds, 10, 180),
                    Secret = Unprotect(stored.ProtectedSecret)
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
            {
                AppLog.Write("KI-Einstellungen konnten nicht geladen werden: " + ex.Message);
                return LoadEnvironmentFallback();
            }
        }
    }

    public static void Save(AiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lock (Sync)
        {
            Directory.CreateDirectory(DirectoryPath);
            var stored = new StoredAiConfiguration
            {
                Enabled = configuration.Enabled,
                Mode = configuration.Mode,
                Endpoint = configuration.Endpoint.Trim(),
                Model = configuration.Model.Trim(),
                SystemInstruction = configuration.SystemInstruction.Trim(),
                TimeoutSeconds = Math.Clamp(configuration.TimeoutSeconds, 10, 180),
                ProtectedSecret = Protect(configuration.Secret)
            };
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
            else File.Move(temp, FilePath);
        }
    }

    private static AiConfiguration LoadEnvironmentFallback()
    {
        var endpoint = Environment.GetEnvironmentVariable("NTB_AI_ENDPOINT") ?? "";
        var token = Environment.GetEnvironmentVariable("NTB_AI_TOKEN") ?? "";
        return new AiConfiguration { Enabled = Uri.TryCreate(endpoint, UriKind.Absolute, out _), Mode = AiProviderMode.NtbProxy, Endpoint = endpoint, Secret = token };
    }

    private static string Protect(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
    }

    private static string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), Entropy, DataProtectionScope.CurrentUser));
    }

    private sealed class StoredAiConfiguration
    {
        public bool Enabled { get; set; }
        public AiProviderMode Mode { get; set; }
        public string? Endpoint { get; set; }
        public string? Model { get; set; }
        public string? SystemInstruction { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
        public string? ProtectedSecret { get; set; }
    }
}
