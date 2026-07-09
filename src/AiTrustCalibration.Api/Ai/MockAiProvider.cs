using System.Text.Json;

namespace AiTrustCalibration.Api.Ai;

public sealed class MockAiProvider : IAiProvider
{
    private readonly string _profile;

    public MockAiProvider(string profile)
    {
        _profile = profile;
    }

    public string Key => $"mock-{_profile}";
    public string DisplayName => $"Mock {_profile.ToUpperInvariant()}";
    public string ModelId => $"mock-pilot-{_profile}";
    public bool Enabled => true;
    public bool Ready => true;
    public string ReasoningMode => "mock";

    public Task<AiProviderResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startedAt = DateTimeOffset.UtcNow;

        var text = _profile switch
        {
            "a" => $"Пилотный mock-ответ. Я вижу задачу: {request.Prompt}\n\nДля реального исследования этот текст будет заменён точным ответом настроенного AI-провайдера.",
            "b" => $"Это второй тестовый ответ для проверки слепого A/B/C-потока. Задача сформулирована так: {request.Prompt}\n\nНедостаток данных: mock provider не решает предметную задачу.",
            _ => $"Третий локальный ответ. Его назначение — проверить random blind assignment, оценку и reveal до подключения API-ключей.\n\nИсходная задача: {request.Prompt}"
        };

        var raw = JsonSerializer.Serialize(new
        {
            mock = true,
            profile = _profile,
            prompt = request.Prompt,
            response = text
        });

        return Task.FromResult(new AiProviderResult(
            Key,
            DisplayName,
            ModelId,
            ReasoningMode,
            startedAt,
            DateTimeOffset.UtcNow,
            text,
            false,
            Array.Empty<string>(),
            raw));
    }
}
