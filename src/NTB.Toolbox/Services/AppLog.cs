namespace NTB.Toolbox.Services;

internal static class AppLog
{
    private static readonly List<string> EntriesInternal = [];

    public static event Action<string>? EntryAdded;

    public static IReadOnlyList<string> Entries => EntriesInternal;

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
        EntriesInternal.Add(line);
        EntryAdded?.Invoke(line);
    }
}
