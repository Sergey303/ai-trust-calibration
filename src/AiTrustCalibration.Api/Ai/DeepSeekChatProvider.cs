using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiTrustCalibration.Api.Ai;

public sealed class DeepSeekChatProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeepSeekOptions _options;

    public DeepSeekChatProvider(IHttpClientFactory httpClientFactory, IOptions<AiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.DeepSeek;
    }

    public string Key => "deepseek";
    public string DisplayName => "DeepSeek";
    public string ModelId => _options.Model;
    public bool Enabled => _options.Enabled;
    public bool Ready => Enabled && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.Model);
    public string ReasoningMode => _options.ThinkingEnabled
        ? $"thinking:{(_options.ReasoningEffort.Length == 0 ? "provider-default" : _options.ReasoningEffort)}"
        : "non-thinking";

    public async Task<AiProviderResult> GenerateAsync(AiGenerationRequest request, CancellationToken cancellationToken)
    {
        if (!Ready)
        {
            throw new InvalidOperationException("DeepSeek provider is enabled but not fully configured.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var client = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), "chat/completions"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = new[]
            {
                new { role = "system", content = request.ResearchInstruction },
                new { role = "user", content = request.Prompt }
            },
            ["stream"] = false
        };

        if (_options.ThinkingEnabled)
        {
            payload["thinking"] = new { type = "enabled" };
        }

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            payload["reasoning_effort"] = _options.ReasoningEffort;
        }

        httpRequest.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"DeepSeek returned {(int)response.StatusCode}: {Limit(rawJson, 1000)}");
        }

        var responseText = ExtractChatText(rawJson);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("DeepSeek response did not contain choices[0].message.content.");
        }

        return new AiProviderResult(
            Key, DisplayName, ModelId, ReasoningMode, startedAt, DateTimeOffset.UtcNow,
            responseText, false, Array.Empty<string>(), rawJson);
    }

    private static string ExtractChatText(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        return document.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";
    private static string Limit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
