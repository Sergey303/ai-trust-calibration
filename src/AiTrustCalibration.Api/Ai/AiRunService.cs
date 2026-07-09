using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace AiTrustCalibration.Api.Ai;

public sealed class AiRunService
{
    private readonly IReadOnlyList<IAiProvider> _providers;
    private readonly StudyStore _store;
    private readonly AiOptions _options;

    public AiRunService(
        IEnumerable<IAiProvider> providers,
        StudyStore store,
        IOptions<AiOptions> options)
    {
        _providers = providers.ToArray();
        _store = store;
        _options = options.Value;
    }

    public IReadOnlyList<AiProviderStatus> GetProviderStatuses() =>
        _providers
            .Select(x => new AiProviderStatus(
                x.Key,
                x.DisplayName,
                x.ModelId,
                x.Enabled,
                x.Ready))
            .ToArray();

    public async Task<GenerationSummaryDto> GenerateAsync(
        StudyTask task,
        CancellationToken cancellationToken)
    {
        var existingAssignments = _store.GetAssignments(task.Id);
        if (existingAssignments.Count > 0)
        {
            return new GenerationSummaryDto(task.Id, existingAssignments.Count, Array.Empty<GenerationFailureDto>());
        }

        var readyProviders = _providers.Where(x => x.Ready).ToArray();
        if (readyProviders.Length == 0)
        {
            throw new InvalidOperationException("No AI provider is ready.");
        }

        var request = new AiGenerationRequest(task.Prompt, _options.ResearchInstruction);
        var generationTasks = readyProviders.Select(provider => GenerateOneAsync(provider, task.Id, request, cancellationToken));
        var completed = await Task.WhenAll(generationTasks);

        var successfulRuns = completed
            .Where(x => x.Run.Succeeded)
            .Select(x => x.Run)
            .ToArray();

        if (successfulRuns.Length == 0)
        {
            throw new InvalidOperationException("All AI provider calls failed.");
        }

        Shuffle(successfulRuns);
        var assignments = successfulRuns
            .Select((run, index) => new BlindAssignment
            {
                TaskId = task.Id,
                Label = ToLabel(index),
                RunId = run.Id
            })
            .ToArray();

        var storedAssignments = _store.SetAssignmentsOnce(task.Id, assignments);
        var failures = completed
            .Where(x => !x.Run.Succeeded)
            .Select(x => new GenerationFailureDto(x.Run.ProviderKey, x.Run.Error ?? "Unknown provider error."))
            .ToArray();

        return new GenerationSummaryDto(task.Id, storedAssignments.Count, failures);
    }

    private async Task<(IAiProvider Provider, AiRun Run)> GenerateOneAsync(
        IAiProvider provider,
        Guid taskId,
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var fallbackStartedAt = DateTimeOffset.UtcNow;
        try
        {
            var result = await provider.GenerateAsync(request, cancellationToken);
            var run = _store.AddRun(new AiRun
            {
                TaskId = taskId,
                ProviderKey = result.ProviderKey,
                ProviderDisplayName = result.ProviderDisplayName,
                ModelId = result.ModelId,
                ReasoningMode = result.ReasoningMode,
                StartedAtUtc = result.StartedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                Prompt = request.Prompt,
                Succeeded = true,
                ResponseText = result.ResponseText,
                WebUsed = result.WebUsed,
                ToolsUsed = result.ToolsUsed,
                RawProviderResponseJson = result.RawProviderResponseJson
            });

            return (provider, run);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var run = _store.AddRun(new AiRun
            {
                TaskId = taskId,
                ProviderKey = provider.Key,
                ProviderDisplayName = provider.DisplayName,
                ModelId = provider.ModelId,
                ReasoningMode = provider.ReasoningMode,
                StartedAtUtc = fallbackStartedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Prompt = request.Prompt,
                Succeeded = false,
                ResponseText = string.Empty,
                WebUsed = false,
                ToolsUsed = Array.Empty<string>(),
                RawProviderResponseJson = string.Empty,
                Error = exception.Message
            });

            return (provider, run);
        }
    }

    private static void Shuffle<T>(T[] items)
    {
        for (var index = items.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }

    private static string ToLabel(int index)
    {
        if (index is < 0 or >= 26)
        {
            throw new InvalidOperationException("Pilot supports up to 26 blind answers per task.");
        }

        return ((char)('A' + index)).ToString();
    }
}
