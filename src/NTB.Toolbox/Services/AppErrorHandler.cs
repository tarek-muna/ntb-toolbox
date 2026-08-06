namespace NTB.Toolbox.Services;

internal static class AppErrorHandler
{
    public static void Handle(Exception exception, string context)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        AppLog.Write($"Fehler in {context}: {exception}");

        MessageBox.Show(
            $"Die Aktion konnte nicht abgeschlossen werden.\r\n\r\nBereich: {context}\r\nFehler: {message}\r\n\r\nWeitere Details stehen im Toolbox-Protokoll.",
            "NTB Toolbox – Fehler",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
