# AI run contract

## Purpose

The AI integration exists to produce reproducible, auditable model runs for a blind comparison. Provider convenience must not weaken the research invariants.

## Common logical request

Every provider receives:

- the exact participant task prompt;
- the same research instruction;
- a fresh stateless context;
- no participant profile, memory or earlier conversation.

Research instruction:

> Ответь на профессиональную задачу максимально точно. Не выдумывай отсутствующие факты. Если данных недостаточно для уверенного вывода, явно укажи, каких данных не хватает. Не утверждай, что выполнил действия или проверки, которых фактически не выполнял.

## Provider adapters

Provider-specific HTTP contracts stay inside adapters.

Current targets:

- `OpenAiResponsesProvider` — OpenAI Responses API.
- `DeepSeekChatProvider` — DeepSeek OpenAI-compatible chat endpoint with provider-specific thinking settings.
- `OpenAiCompatibleChatProvider` — configurable chat-completions adapter for a provider exposing an OpenAI-compatible endpoint; intended as the initial integration seam for Yandex AI Studio after credentials and the exact live contract are verified.
- `MockAiProvider` — local pilot flow without credentials.

Do not force OpenAI Responses and chat-completions providers through one synthetic universal payload.

## Stored run metadata

Every generation stores:

- `runId`;
- `taskId`;
- `providerKey`;
- `providerDisplayName`;
- `modelId`;
- `reasoningMode`;
- `startedAtUtc`;
- `completedAtUtc`;
- exact `prompt`;
- exact returned response text;
- `webUsed`;
- `toolsUsed`;
- raw provider response JSON;
- failure details when the call failed.

Raw provider JSON is research/audit data and is never part of the blind participant DTO.

## Configuration

Credentials come only from environment variables or .NET user-secrets.

Suggested variables:

```text
Ai__OpenAI__ApiKey
Ai__OpenAI__Model
Ai__DeepSeek__ApiKey
Ai__DeepSeek__Model
Ai__Yandex__ApiKey
Ai__Yandex__Model
Ai__Yandex__BaseUrl
Ai__Yandex__CompletionPath
Ai__Yandex__AuthorizationScheme
```

A real provider is enabled only when both `Enabled=true` and required configuration is present. Missing credentials must not crash startup.

## Web and tools

Pilot default: web and provider tools disabled.

A provider adapter must not silently enable web search, browsing, file search or agent tools. If a future study arm enables tools, this becomes an explicit protocol field and run metadata.

## Failure handling

A failed provider call is not converted into an empty blind answer.

Generation endpoint returns a failed run summary to the organizer flow and creates blind assignments only from successful runs. Pilot review decides whether the task is rerun or excluded. Never retry only the provider whose answer was judged inconvenient.

## Token/model changes

Model identifiers are configuration, not code constants. Before the main study starts, exact provider, model id, reasoning setting and run policy are frozen in the public protocol. A provider-side silent alias change is a study risk and must be recorded if discovered.
