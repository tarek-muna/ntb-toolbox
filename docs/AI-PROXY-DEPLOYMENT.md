# NTB Toolbox AI Proxy

Der Proxy stellt den von der Desktop-App erwarteten Endpunkt `POST /v1/ask` bereit und hält den OpenAI-Schlüssel ausschließlich serverseitig.

## Konfiguration

Erforderlich:

- `OPENAI_API_KEY`: OpenAI-Projekt- oder Service-Account-Schlüssel

Optional:

- `OPENAI_MODEL`: Modellname, Standard `gpt-5`
- `NTB_AI_TOKEN`: eigener Gateway-Token für Toolbox-Clients
- `ASPNETCORE_URLS`: Listener, im Container standardmäßig `http://+:8080`

## Lokal starten

```powershell
$env:OPENAI_API_KEY = "..."
$env:NTB_AI_TOKEN = "..."
dotnet run --project .\src\NTB.Toolbox.AiProxy\NTB.Toolbox.AiProxy.csproj
```

Toolbox konfigurieren:

```powershell
[Environment]::SetEnvironmentVariable("NTB_AI_ENDPOINT", "https://proxy.example/v1/ask", "Machine")
[Environment]::SetEnvironmentVariable("NTB_AI_TOKEN", "...", "Machine")
```

## Container

```powershell
docker build -t ntb-toolbox-ai-proxy .\src\NTB.Toolbox.AiProxy
docker run --rm -p 8080:8080 `
  -e OPENAI_API_KEY="..." `
  -e NTB_AI_TOKEN="..." `
  ntb-toolbox-ai-proxy
```

## Betrieb

- ausschließlich hinter HTTPS veröffentlichen
- Schlüssel aus Secret Store oder geschützter Umgebungsvariable laden
- keine Fragen oder Antworten standardmäßig protokollieren
- ausgehenden Netzwerkzugriff auf `api.openai.com` begrenzen
- Kosten- und Nutzungsgrenzen im OpenAI-Projekt konfigurieren
- Reverse Proxy/WAF und zusätzliche organisationsweite Rate Limits verwenden
- Token regelmäßig rotieren

## Schnittstelle

Anfrage:

```json
{ "question": "Wie prüfe ich DNS-Probleme unter Windows?" }
```

Antwort:

```json
{ "answer": "...", "model": "gpt-5" }
```
