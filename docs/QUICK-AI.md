# Schnellfrage (KI)

Die Desktop-Anwendung enthält bewusst keinen OpenAI-API-Schlüssel. Anwender müssen sich in der Toolbox nicht anmelden; die Anwendung sendet Fragen an einen internen NTB-KI-Proxy.

## Konfiguration der Clients

```powershell
[Environment]::SetEnvironmentVariable(
  "NTB_AI_ENDPOINT",
  "https://ai-gateway.example.internal/v1/ask",
  "Machine")
```

Optional kann ein technischer, rotierbarer Proxy-Token gesetzt werden:

```powershell
[Environment]::SetEnvironmentVariable(
  "NTB_AI_TOKEN",
  "<managed-proxy-token>",
  "Machine")
```

Der Wert ist kein OpenAI-Schlüssel. Er dient ausschließlich zur Absicherung des eigenen Gateways und sollte über Geräteverwaltung oder Secret Management verteilt werden.

## Request-Vertrag

```json
{
  "question": "Warum löst DNS auf, aber Ping schlägt fehl?",
  "system_instruction": "Du bist der kurze, präzise IT-Assistent ..."
}
```

## Response-Vertrag

Erfolg:

```json
{
  "answer": "Eine mögliche Ursache ist eine blockierte ICMP-Regel ..."
}
```

Fehler:

```json
{
  "error": "Monatliches Kontingent erreicht."
}
```

## Anforderungen an den Proxy

- OpenAI-Schlüssel ausschließlich serverseitig speichern.
- Responses API serverseitig aufrufen.
- Eingaben und Antworten nach NTB-Datenschutzvorgaben behandeln.
- Rate Limits, Kostenlimit und maximale Antwortlänge erzwingen.
- Keine Passwörter, privaten Schlüssel oder personenbezogenen Daten annehmen.
- Nutzungs- und Fehlerprotokolle ohne vollständige vertrauliche Inhalte führen.
- Schlüssel regelmäßig rotieren und getrennte Projekt-Schlüssel verwenden.
