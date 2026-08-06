using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NTB.Toolbox.Services;

internal static class QuickAiService
{
    public static bool IsConfigured
    {
        get
        {
            var configuration = AiConfigurationService.Load();
            return configuration.Enabled && Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out _);
        }
    }

    public static Task<string> AskAsync(string question, CancellationToken cancellationToken = default) =>
        AskAsync(question, AiConfigurationService.Load(), cancellationToken);

    public static async Task<string> AskAsync(string question, AiConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Bitte eine Frage eingeben.", nameof(question));
        if (!configuration.Enabled) throw new InvalidOperationException("Die KI-Funktionen sind deaktiviert. Öffne Einstellungen → KI-Einstellungen.");
        if (!Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Die KI-Verbindung ist nicht vollständig konfiguriert.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(configuration.TimeoutSeconds, 10, 180)) };
        return configuration.Mode == AiProviderMode.OpenAi
            ? await AskOpenAiAsync(client, endpoint, configuration, question.Trim(), cancellationToken)
            : await AskProxyAsync(client, endpoint, configuration, question.Trim(), cancellationToken);
    }

    private static async Task<string> AskProxyAsync(HttpClient client, Uri endpoint, AiConfiguration configuration, string question, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new QuickAiRequest(question, configuration.SystemInstruction))
        };
        if (!string.IsNullOrWhiteSpace(configuration.Secret))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.Secret.Trim());

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<QuickAiResponse>(cancellationToken: cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(result?.Error ?? $"KI-Backend antwortete mit HTTP {(int)response.StatusCode}.");
        if (string.IsNullOrWhiteSpace(result?.Answer)) throw new InvalidOperationException("Das KI-Backend hat keine Antwort geliefert.");
        return result.Answer.Trim();
    }

    private static async Task<string> AskOpenAiAsync(HttpClient client, Uri endpoint, AiConfiguration configuration, string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.Secret)) throw new InvalidOperationException("Für die direkte OpenAI API ist ein API-Key erforderlich.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = string.IsNullOrWhiteSpace(configuration.Model) ? "gpt-5" : configuration.Model,
                instructions = configuration.SystemInstruction,
                input = question
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.Secret.Trim());
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                using var errorDocument = JsonDocument.Parse(json);
                var message = errorDocument.RootElement.GetProperty("error").GetProperty("message").GetString();
                throw new HttpRequestException(message ?? $"OpenAI antwortete mit HTTP {(int)response.StatusCode}.");
            }
            catch (JsonException) { throw new HttpRequestException($"OpenAI antwortete mit HTTP {(int)response.StatusCode}."); }
        }
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("output_text", out var outputText) && !string.IsNullOrWhiteSpace(outputText.GetString()))
            return outputText.GetString()!.Trim();
        if (document.RootElement.TryGetProperty("output", out var output))
        {
            foreach (var item in output.EnumerateArray())
                if (item.TryGetProperty("content", out var content))
                    foreach (var part in content.EnumerateArray())
                        if (part.TryGetProperty("text", out var text) && !string.IsNullOrWhiteSpace(text.GetString())) return text.GetString()!.Trim();
        }
        throw new InvalidOperationException("OpenAI hat keine Textantwort geliefert.");
    }

    private sealed record QuickAiRequest([property: JsonPropertyName("question")] string Question, [property: JsonPropertyName("system_instruction")] string SystemInstruction);
    private sealed record QuickAiResponse([property: JsonPropertyName("answer")] string? Answer, [property: JsonPropertyName("error")] string? Error);
}
