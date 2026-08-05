using NTB.Toolbox.Services;

namespace NTB.Toolbox.Modules;

internal sealed class QuickAiModule : IToolboxModule
{
    public string Id => "quick-ai";
    public string Title => "Schnellfrage (KI)";
    public string Category => "Wissen";
    public string Description => "Kurze IT-Fragen ohne Benutzeranmeldung über den sicheren NTB-KI-Proxy beantworten.";
    public bool RequiresAdministrator => false;
    public IReadOnlyCollection<string> Keywords => ["chatgpt", "ki", "ai", "frage", "hilfe", "assistent"];

    public Control CreateView()
    {
        var question = new TextBox
        {
            Dock = DockStyle.Top,
            Multiline = true,
            Height = 90,
            PlaceholderText = "Frage eingeben, z. B. Warum funktioniert DNS, aber Ping nicht?"
        };
        var answer = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Segoe UI", 10),
            Text = QuickAiService.IsConfigured
                ? "Bereit. Die Anfrage wird über den konfigurierten NTB-KI-Proxy verarbeitet."
                : "Nicht konfiguriert. NTB_AI_ENDPOINT muss auf den internen KI-Proxy zeigen."
        };
        var ask = new Button { Text = "Frage senden", AutoSize = true, Height = 36 };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, Height = 36, Enabled = false };
        var clear = new Button { Text = "Leeren", AutoSize = true, Height = 36 };
        var status = new Label { AutoSize = true, Text = QuickAiService.IsConfigured ? "Verbunden konfiguriert" : "Nicht konfiguriert" };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.AddRange([ask, cancel, clear, status]);

        CancellationTokenSource? cancellation = null;
        ask.Click += async (_, _) =>
        {
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            ask.Enabled = false;
            cancel.Enabled = true;
            status.Text = "Antwort wird erstellt …";
            answer.Text = string.Empty;
            AppLog.Write("Schnellfrage an KI-Backend gesendet.");
            try
            {
                answer.Text = await QuickAiService.AskAsync(question.Text, cancellation.Token);
                status.Text = "Antwort erhalten";
                AppLog.Write("KI-Antwort erfolgreich empfangen.");
            }
            catch (OperationCanceledException)
            {
                status.Text = "Abgebrochen";
                answer.Text = "Die Anfrage wurde abgebrochen.";
            }
            catch (Exception ex)
            {
                status.Text = "Fehler";
                answer.Text = "Fehler: " + ex.Message;
                AppLog.Write("KI-Anfrage fehlgeschlagen: " + ex.Message);
            }
            finally
            {
                ask.Enabled = true;
                cancel.Enabled = false;
            }
        };
        cancel.Click += (_, _) => cancellation?.Cancel();
        clear.Click += (_, _) => { question.Clear(); answer.Clear(); status.Text = QuickAiService.IsConfigured ? "Bereit" : "Nicht konfiguriert"; };
        question.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter && ask.Enabled)
            {
                e.SuppressKeyPress = true;
                ask.PerformClick();
            }
        };

        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        panel.Controls.Add(answer);
        panel.Controls.Add(buttons);
        panel.Controls.Add(question);
        return panel;
    }
}
