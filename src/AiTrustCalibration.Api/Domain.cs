namespace AiTrustCalibration.Api;

public sealed class Participant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required int PriorErrorEstimate { get; init; }
    public required string FirstModelAssociation { get; init; }
    public required IReadOnlyList<string> UsedModelsLastThreeMonths { get; init; }
    public required string ChatGptUsageFrequency { get; init; }
    public required string VerificationHabit { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public PostSurvey? PostSurvey { get; set; }
}

public sealed class StudyTask
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ParticipantId { get; init; }
    public required string Prompt { get; init; }
    public required IReadOnlyList<string> ExpectedCore { get; init; }
    public required IReadOnlyList<string> CriticalErrors { get; init; }
    public required IReadOnlyList<string> DisputedAreas { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class AiRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TaskId { get; init; }
    public required string ProviderKey { get; init; }
    public required string ProviderDisplayName { get; init; }
    public required string ModelId { get; init; }
    public required string ReasoningMode { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public required string Prompt { get; init; }
    public required bool Succeeded { get; init; }
    public required string ResponseText { get; init; }
    public required bool WebUsed { get; init; }
    public required IReadOnlyList<string> ToolsUsed { get; init; }
    public required string RawProviderResponseJson { get; init; }
    public string? Error { get; init; }
}

public sealed class BlindAssignment
{
    public required Guid TaskId { get; init; }
    public required string Label { get; init; }
    public required Guid RunId { get; init; }
}

public sealed class BlindEvaluation
{
    public required Guid TaskId { get; init; }
    public required string Label { get; init; }
    public required int Severity { get; init; }
    public required bool HallucinatedFact { get; init; }
    public required bool AdmittedInsufficientData { get; init; }
    public required int VerificationBurden { get; init; }
    public required string Rationale { get; init; }
    public DateTimeOffset SubmittedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PostSurvey
{
    public required int PosteriorErrorEstimate { get; init; }
    public required string VerificationStrategy { get; init; }
    public required string TrustChange { get; init; }
    public required string Comment { get; init; }
    public DateTimeOffset SubmittedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
