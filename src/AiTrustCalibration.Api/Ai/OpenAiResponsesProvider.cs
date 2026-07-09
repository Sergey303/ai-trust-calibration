using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiTrustCalibration.Api.Ai;

public sealed class OpenAiResponsesProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProviderOptions _options;

    public OpenAiResponsesProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.OpenAI;
    }

    public string Key => "openai";
    public string DisplayName => "OpenAI";
    public string ModelId => _options.Model;
    public bool Enabled => _options.Enabled;
    public bool Ready => Enabled
        && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.Model);
    public string ReasoningMode => string.IsNullOrWhiteSpace(_options.ReasoningEffort)
        ? "provider-default"
        : _options.ReasoningEffort;

    public async Task<AiProviderResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureReady();
        var startedAt = DateTimeOffset.UtcNow;
        var client = _httpClientFactory.CreateClient();

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), "responses"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["instructions"] = request.ResearchInstruction,
            ["input"] = request.Prompt,
            ["store"] = false
        };

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            payload["reasoning"] = new { effort = _options.ReasoningEffort };
        }

        httpRequest.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI returned {(int)response.StatusCode}: {Limit(rawJson, 1000)}");
        }

        var responseText = ExtractResponseText(rawJson);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("OpenAI response did not contain output_text.");
        }

        return new AiProviderResult(
            Key,
            DisplayName,
            ModelId,
            ReasoningMode,
            startedAt,
            DateTimeOffset.UtcNow,
            responseText,
            false,
            Array.Empty<string>(),
            rawJson);
    }

    private void EnsureReady()
    {
        if (!Ready)
        {
            throw new InvalidOperationException("OpenAI provider is enabled but not fully configured.");
        }
    }

    private static string ExtractResponseText(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        if (!document.RootElement.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var chunks = new List<string>();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    chunks.Add(text.GetString()!);
                }
            }
        }

        return string.Join("\n", chunks);
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : value + "/";

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
