# AI credentials and provider activation

Do not paste provider tokens into `appsettings.json` and do not commit them.

Run commands from `src/AiTrustCalibration.Api` in PowerShell.

## OpenAI

```powershell
dotnet user-secrets set "Ai:OpenAI:ApiKey" "YOUR_OPENAI_KEY"
dotnet user-secrets set "Ai:OpenAI:Model" "EXACT_MODEL_ID"
dotnet user-secrets set "Ai:OpenAI:Enabled" "true"
```

`Ai:OpenAI:ReasoningEffort` defaults to `high` in `appsettings.json`. Freeze the exact model id and reasoning setting in the study protocol before the main sample starts.

## DeepSeek

```powershell
dotnet user-secrets set "Ai:DeepSeek:ApiKey" "YOUR_DEEPSEEK_KEY"
dotnet user-secrets set "Ai:DeepSeek:Model" "deepseek-v4-pro"
dotnet user-secrets set "Ai:DeepSeek:Enabled" "true"
```

Current repository defaults request thinking mode with `reasoning_effort=high`.

## Yandex AI Studio

The repository intentionally does not guess a live endpoint/auth contract. After the API key is available, verify the exact OpenAI-compatible contract and configure it explicitly:

```powershell
dotnet user-secrets set "Ai:Yandex:ApiKey" "YOUR_YANDEX_KEY"
dotnet user-secrets set "Ai:Yandex:Model" "EXACT_MODEL_ID"
dotnet user-secrets set "Ai:Yandex:BaseUrl" "VERIFIED_BASE_URL"
dotnet user-secrets set "Ai:Yandex:CompletionPath" "VERIFIED_COMPLETION_PATH"
dotnet user-secrets set "Ai:Yandex:AuthorizationHeaderName" "VERIFIED_HEADER_NAME"
dotnet user-secrets set "Ai:Yandex:AuthorizationScheme" "VERIFIED_SCHEME"
dotnet user-secrets set "Ai:Yandex:Enabled" "true"
```

Then make one organizer-only contract verification call and inspect the stored raw provider response before accepting the adapter for research runs.

## Provider-set safety

- With zero ready real providers, the API uses three mock providers for local A/B/C flow.
- With exactly one ready real provider, generation is rejected.
- With two or more ready real providers, only real providers run; mock providers are automatically excluded.
- At least two provider calls must succeed before blind assignments are created.

This prevents local mock answers from contaminating a real comparison and prevents a one-answer flow from being mistaken for a blind model comparison.
