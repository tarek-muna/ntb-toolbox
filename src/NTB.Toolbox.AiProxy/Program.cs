using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var gatewayToken = Environment.GetEnvironmentVariable("NTB_AI_TOKEN");
if (string.IsNullOrWhiteSpace(gatewayToken))
    throw new InvalidOperationException("NTB_AI_TOKEN muss gesetzt sein. Der KI-Proxy startet aus Sicherheitsgründen nicht ohne Zugriffstoken.");

builder.Services.AddHttpClient("openai", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/v1/ask", async (HttpContext context, AskRequest request, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    var authorization = context.Request.Headers.Authorization.ToString();
    var expectedAuthorization = $"Bearer {gatewayToken}";
    if (!FixedTimeEquals(authorization, expectedAuthorization))
        return Results.Unauthorized();

    var question = request.Question?.Trim();
    if (string.IsNullOrWhiteSpace(question))
        return Results.BadRequest(new { error = "Die Frage darf nicht leer sein." });

    if (question.Length > 4000)
        return Results.BadRequest(new { error = "Die Frage darf maximal 4000 Zeichen enthalten." });

    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Problem("OPENAI_API_KEY ist auf dem Server nicht konfiguriert.", statusCode: 503);

    var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-5";
    var payload = new
    {
        model,
        instructions = "Du bist ein kompakter IT-Assistent für Fachinformatiker Systemintegration. Antworte auf Deutsch, praxisnah und sicher. Weise bei riskanten Änderungen auf Backup, Testumgebung und notwendige Berechtigungen hin. Erfinde keine Befehlsausgaben oder Umgebungsdetails.",
        input = question,
        max_output_tokens = 900
    };

    using var message = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    var client = httpClientFactory.CreateClient("openai");
    using var response = await client.SendAsync(message, cancellationToken);
    var json = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        app.Logger.LogWarning("OpenAI request failed with status {StatusCode}.", (int)response.StatusCode);
        return Results.Problem("Der KI-Dienst konnte die Anfrage nicht beantworten.", statusCode: 502);
    }

    using var document = JsonDocument.Parse(json);
    var answer = ExtractOutputText(document.RootElement);
    if (string.IsNullOrWhiteSpace(answer))
        return Results.Problem("Der KI-Dienst hat keine Textantwort geliefert.", statusCode: 502);

    return Results.Ok(new AskResponse(answer, model));
});

app.Run();

static bool FixedTimeEquals(string actual, string expected)
{
    var actualBytes = Encoding.UTF8.GetBytes(actual);
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    return actualBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
}

static string ExtractOutputText(JsonElement root)
{
    if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        return string.Empty;

    var parts = new List<string>();
    foreach (var item in output.EnumerateArray())
    {
        if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            continue;

        foreach (var entry in content.EnumerateArray())
        {
            if (entry.TryGetProperty("type", out var type) && type.GetString() == "output_text" &&
                entry.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                parts.Add(text.GetString()!);
        }
    }

    return string.Join(Environment.NewLine, parts);
}

internal sealed record AskRequest(string? Question);
internal sealed record AskResponse(string Answer, string Model);
