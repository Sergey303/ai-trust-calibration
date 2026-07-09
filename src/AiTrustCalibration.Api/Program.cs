using AiTrustCalibration.Api;
using AiTrustCalibration.Api.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<StudyStore>();
builder.Services.AddSingleton<IAiProvider>(_ => new MockAiProvider("a"));
builder.Services.AddSingleton<IAiProvider>(_ => new MockAiProvider("b"));
builder.Services.AddSingleton<IAiProvider>(_ => new MockAiProvider("c"));
builder.Services.AddSingleton<IAiProvider, OpenAiResponsesProvider>();
builder.Services.AddSingleton<IAiProvider, DeepSeekChatProvider>();
builder.Services.AddSingleton<IAiProvider, OpenAiCompatibleChatProvider>();
builder.Services.AddSingleton<AiRunService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", phase = "pilot" }));

app.MapGet("/api/providers", (AiRunService aiRuns) => Results.Ok(aiRuns.GetProviderStatuses()));

app.MapPost("/api/participants", (CreateParticipantRequest request, StudyStore store) =>
{
    if (!InPercentRange(request.PriorErrorEstimate))
    {
        return Results.BadRequest("PriorErrorEstimate must be between 0 and 100.");
    }

    if (string.IsNullOrWhiteSpace(request.FirstModelAssociation)
        || string.IsNullOrWhiteSpace(request.ChatGptUsageFrequency)
        || string.IsNullOrWhiteSpace(request.VerificationHabit))
    {
        return Results.BadRequest("Pre-survey text fields are required.");
    }

    var participant = store.AddParticipant(new Participant
    {
        PriorErrorEstimate = request.PriorErrorEstimate,
        FirstModelAssociation = request.FirstModelAssociation.Trim(),
        UsedModelsLastThreeMonths = Clean(request.UsedModelsLastThreeMonths),
        ChatGptUsageFrequency = request.ChatGptUsageFrequency.Trim(),
        VerificationHabit = request.VerificationHabit.Trim()
    });

    return Results.Created($"/api/participants/{participant.Id}", new { participant.Id });
});

app.MapPost("/api/tasks", (CreateTaskRequest request, StudyStore store) =>
{
    if (store.GetParticipant(request.ParticipantId) is null)
    {
        return Results.NotFound("Participant was not found.");
    }

    var expectedCore = Clean(request.ExpectedCore);
    var criticalErrors = Clean(request.CriticalErrors);
    var disputedAreas = Clean(request.DisputedAreas);

    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest("Prompt is required.");
    }

    if (expectedCore.Count is < 3 or > 7)
    {
        return Results.BadRequest("ExpectedCore must contain 3 to 7 items.");
    }

    if (criticalErrors.Count == 0)
    {
        return Results.BadRequest("At least one critical error criterion is required.");
    }

    var task = store.AddTask(new StudyTask
    {
        ParticipantId = request.ParticipantId,
        Prompt = request.Prompt.Trim(),
        ExpectedCore = expectedCore,
        CriticalErrors = criticalErrors,
        DisputedAreas = disputedAreas
    });

    return Results.Created($"/api/tasks/{task.Id}", new { task.Id });
});

app.MapPost("/api/tasks/{taskId:guid}/generate", async (
    Guid taskId,
    StudyStore store,
    AiRunService aiRuns,
    CancellationToken cancellationToken) =>
{
    var task = store.GetTask(taskId);
    if (task is null)
    {
        return Results.NotFound("Task was not found.");
    }

    try
    {
        var result = await aiRuns.GenerateAsync(task, cancellationToken);
        return Results.Ok(result);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
    }
});

app.MapGet("/api/tasks/{taskId:guid}/blind", (Guid taskId, StudyStore store) =>
{
    var task = store.GetTask(taskId);
    if (task is null)
    {
        return Results.NotFound("Task was not found.");
    }

    var assignments = store.GetAssignments(taskId);
    if (assignments.Count == 0)
    {
        return Results.Conflict("Task has not been generated yet.");
    }

    var evaluatedLabels = store.GetEvaluations(taskId)
        .Select(x => x.Label)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var answers = assignments
        .OrderBy(x => x.Label)
        .Select(assignment =>
        {
            var run = store.GetRun(assignment.RunId)
                ?? throw new InvalidOperationException("Blind assignment references a missing run.");
            return new BlindAnswerDto(
                assignment.Label,
                run.ResponseText,
                evaluatedLabels.Contains(assignment.Label));
        })
        .ToArray();

    return Results.Ok(new BlindTaskDto(
        task.Id,
        task.Prompt,
        task.ExpectedCore,
        task.CriticalErrors,
        task.DisputedAreas,
        answers,
        store.CanReveal(taskId)));
});

app.MapPost("/api/tasks/{taskId:guid}/evaluations", (
    Guid taskId,
    SubmitEvaluationRequest request,
    StudyStore store) =>
{
    if (store.GetTask(taskId) is null)
    {
        return Results.NotFound("Task was not found.");
    }

    var label = request.Label.Trim().ToUpperInvariant();
    var assignmentExists = store.GetAssignments(taskId)
        .Any(x => x.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
    if (!assignmentExists)
    {
        return Results.BadRequest("Unknown blind answer label.");
    }

    if (request.Severity is < 0 or > 3 || request.VerificationBurden is < 0 or > 3)
    {
        return Results.BadRequest("Severity and VerificationBurden must be between 0 and 3.");
    }

    if (string.IsNullOrWhiteSpace(request.Rationale))
    {
        return Results.BadRequest("Evaluation rationale is required.");
    }

    try
    {
        store.AddEvaluation(new BlindEvaluation
        {
            TaskId = taskId,
            Label = label,
            Severity = request.Severity,
            HallucinatedFact = request.HallucinatedFact,
            AdmittedInsufficientData = request.AdmittedInsufficientData,
            VerificationBurden = request.VerificationBurden,
            Rationale = request.Rationale.Trim()
        });

        return Results.Ok(new { canReveal = store.CanReveal(taskId) });
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(exception.Message);
    }
});

app.MapGet("/api/tasks/{taskId:guid}/reveal", (Guid taskId, StudyStore store) =>
{
    if (store.GetTask(taskId) is null)
    {
        return Results.NotFound("Task was not found.");
    }

    if (!store.CanReveal(taskId))
    {
        return Results.Conflict("All blind answers must be evaluated before reveal.");
    }

    var reveal = store.GetAssignments(taskId)
        .OrderBy(x => x.Label)
        .Select(assignment =>
        {
            var run = store.GetRun(assignment.RunId)
                ?? throw new InvalidOperationException("Blind assignment references a missing run.");
            return new RevealItemDto(
                assignment.Label,
                run.ProviderDisplayName,
                run.ModelId,
                run.ReasoningMode,
                run.CompletedAtUtc);
        })
        .ToArray();

    return Results.Ok(reveal);
});

app.MapPost("/api/participants/{participantId:guid}/post-survey", (
    Guid participantId,
    SubmitPostSurveyRequest request,
    StudyStore store) =>
{
    if (!InPercentRange(request.PosteriorErrorEstimate))
    {
        return Results.BadRequest("PosteriorErrorEstimate must be between 0 and 100.");
    }

    try
    {
        store.SetPostSurvey(participantId, new PostSurvey
        {
            PosteriorErrorEstimate = request.PosteriorErrorEstimate,
            VerificationStrategy = request.VerificationStrategy.Trim(),
            TrustChange = request.TrustChange.Trim(),
            Comment = request.Comment.Trim()
        });
        return Results.NoContent();
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(exception.Message);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(exception.Message);
    }
});

app.Run();

static bool InPercentRange(int value) => value is >= 0 and <= 100;

static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
    (values ?? Array.Empty<string>())
        .Select(x => x.Trim())
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
