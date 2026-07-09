namespace AiTrustCalibration.Api;

public sealed record CreateParticipantRequest(
    int PriorErrorEstimate,
    string FirstModelAssociation,
    IReadOnlyList<string> UsedModelsLastThreeMonths,
    string ChatGptUsageFrequency,
    string VerificationHabit);

public sealed record CreateTaskRequest(
    Guid ParticipantId,
    string Prompt,
    IReadOnlyList<string> ExpectedCore,
    IReadOnlyList<string> CriticalErrors,
    IReadOnlyList<string> DisputedAreas);

public sealed record SubmitEvaluationRequest(
    string Label,
    int Severity,
    bool HallucinatedFact,
    bool AdmittedInsufficientData,
    int VerificationBurden,
    string Rationale);

public sealed record SubmitPostSurveyRequest(
    int PosteriorErrorEstimate,
    string VerificationStrategy,
    string TrustChange,
    string Comment);

public sealed record BlindAnswerDto(string Label, string Content, bool Evaluated);

public sealed record BlindTaskDto(
    Guid TaskId,
    string Prompt,
    IReadOnlyList<string> ExpectedCore,
    IReadOnlyList<string> CriticalErrors,
    IReadOnlyList<string> DisputedAreas,
    IReadOnlyList<BlindAnswerDto> Answers,
    bool CanReveal);

public sealed record RevealItemDto(
    string Label,
    string ProviderDisplayName,
    string ModelId,
    string ReasoningMode,
    DateTimeOffset CompletedAtUtc);

public sealed record GenerationFailureDto(string ProviderKey, string Error);

public sealed record GenerationSummaryDto(
    Guid TaskId,
    int BlindAnswerCount,
    IReadOnlyList<GenerationFailureDto> Failures);
