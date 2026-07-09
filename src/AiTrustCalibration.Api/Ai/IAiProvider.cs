namespace AiTrustCalibration.Api.Ai;

public sealed record AiGenerationRequest(string Prompt, string ResearchInstruction);

public sealed record AiProviderResult(
    string ProviderKey,
    string ProviderDisplayName,
    string ModelId,
    string ReasoningMode,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string ResponseText,
    bool WebUsed,
    IReadOnlyList<string> ToolsUsed,
    string RawProviderResponseJson);

public sealed record AiProviderStatus(
    string Key,
    string DisplayName,
    string ModelId,
    bool Enabled,
    bool Ready);

public interface IAiProvider
{
    string Key { get; }
    string DisplayName { get; }
    string ModelId { get; }
    bool Enabled { get; }
    bool Ready { get; }
    string ReasoningMode { get; }

    Task<AiProviderResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken);
}
