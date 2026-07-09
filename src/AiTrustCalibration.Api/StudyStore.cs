using System.Collections.Concurrent;

namespace AiTrustCalibration.Api;

public sealed class StudyStore
{
    private readonly ConcurrentDictionary<Guid, Participant> _participants = new();
    private readonly ConcurrentDictionary<Guid, StudyTask> _tasks = new();
    private readonly ConcurrentDictionary<Guid, AiRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<BlindAssignment>> _assignments = new();
    private readonly ConcurrentDictionary<(Guid TaskId, string Label), BlindEvaluation> _evaluations = new();

    public Participant AddParticipant(Participant participant)
    {
        if (!_participants.TryAdd(participant.Id, participant))
        {
            throw new InvalidOperationException("Participant id collision.");
        }

        return participant;
    }

    public Participant? GetParticipant(Guid participantId) =>
        _participants.GetValueOrDefault(participantId);

    public StudyTask AddTask(StudyTask task)
    {
        if (!_participants.ContainsKey(task.ParticipantId))
        {
            throw new KeyNotFoundException("Participant was not found.");
        }

        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException("Task id collision.");
        }

        return task;
    }

    public StudyTask? GetTask(Guid taskId) => _tasks.GetValueOrDefault(taskId);

    public AiRun AddRun(AiRun run)
    {
        _runs[run.Id] = run;
        return run;
    }

    public AiRun? GetRun(Guid runId) => _runs.GetValueOrDefault(runId);

    public IReadOnlyList<AiRun> GetRuns(Guid taskId) =>
        _runs.Values
            .Where(x => x.TaskId == taskId)
            .OrderBy(x => x.StartedAtUtc)
            .ToArray();

    public IReadOnlyList<BlindAssignment> GetAssignments(Guid taskId) =>
        _assignments.GetValueOrDefault(taskId) ?? Array.Empty<BlindAssignment>();

    public IReadOnlyList<BlindAssignment> SetAssignmentsOnce(
        Guid taskId,
        IReadOnlyList<BlindAssignment> assignments)
    {
        return _assignments.GetOrAdd(taskId, assignments);
    }

    public BlindEvaluation AddEvaluation(BlindEvaluation evaluation)
    {
        var key = (evaluation.TaskId, evaluation.Label.ToUpperInvariant());
        if (!_evaluations.TryAdd(key, evaluation))
        {
            throw new InvalidOperationException("This blind answer has already been evaluated.");
        }

        return evaluation;
    }

    public IReadOnlyList<BlindEvaluation> GetEvaluations(Guid taskId) =>
        _evaluations.Values
            .Where(x => x.TaskId == taskId)
            .OrderBy(x => x.Label)
            .ToArray();

    public bool CanReveal(Guid taskId)
    {
        var assignments = GetAssignments(taskId);
        if (assignments.Count == 0)
        {
            return false;
        }

        var evaluatedLabels = GetEvaluations(taskId)
            .Select(x => x.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return assignments.All(x => evaluatedLabels.Contains(x.Label));
    }

    public void SetPostSurvey(Guid participantId, PostSurvey survey)
    {
        var participant = GetParticipant(participantId)
            ?? throw new KeyNotFoundException("Participant was not found.");

        lock (participant)
        {
            if (participant.PostSurvey is not null)
            {
                throw new InvalidOperationException("Post-survey has already been submitted.");
            }

            participant.PostSurvey = survey;
        }
    }
}
