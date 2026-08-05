using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace NTB.Toolbox.Services;

internal static class QuickAiService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };

    public static bool IsConfigured => Uri.TryCreate(Environment.GetEnvironmentVariable("NTB_AI_ENDPOINT"), UriKind.Absolute, out _);

    public static async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Bitte eine Frage eingeben.", nameof(question));

        var endpointValue = Environment.GetEnvironmentVariable("NTB_AI_ENDPOINT");
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Die KI-Verbindung ist nicht konfiguriert. Setze NTB_AI_ENDPOINT auf den internen NTB-KI-Proxy.");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new QuickAiRequest(question.Trim(), "Du bist der kurze, präzise IT-Assistent der NTB Toolbox. Antworte auf Deutsch und nenne bei Unsicherheit klar die Grenzen."))
        };

        var token = Environment.GetEnvironmentVariable("NTB_AI_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<QuickAiResponse>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(result?.Error ?? $"KI-Backend antwortete mit HTTP {(int)response.StatusCode}.");

        if (string.IsNullOrWhiteSpace(result?.Answer))
            throw new InvalidOperationException("Das KI-Backend hat keine Antwort geliefert.");

        return result.Answer.Trim();
    }

    private sealed record QuickAiRequest(
        [property: JsonPropertyName("question")] string Question,
        [property: JsonPropertyName("system_instruction")] string SystemInstruction);

    private sealed record QuickAiResponse(
        [property: JsonPropertyName("answer")] string? Answer,
        [property: JsonPropertyName("error")] string? Error);
}
