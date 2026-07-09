using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiTrustCalibration.Api.Ai;

public sealed class OpenAiCompatibleChatProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CompatibleChatOptions _options;

    public OpenAiCompatibleChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Yandex;
    }

    public string Key => "yandex";
    public string DisplayName => "Yandex AI Studio";
    public string ModelId => _options.Model;
    public bool Enabled => _options.Enabled;
    public bool Ready => Enabled
        && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.Model)
        && !string.IsNullOrWhiteSpace(_options.CompletionPath);
    public string ReasoningMode => string.IsNullOrWhiteSpace(_options.ReasoningEffort)
        ? "provider-default"
        : _options.ReasoningEffort;

    public async Task<AiProviderResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Ready)
        {
            throw new InvalidOperationException("Yandex compatible provider is enabled but not fully configured.");
        }

        var startedAt = DateTimeOffset.UtcNow;
        var client = _httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(EnsureTrailingSlash(_options.BaseUrl)), _options.CompletionPath.TrimStart('/')));

        ApplyAuthorization(httpRequest);

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

        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
        {
            payload["reasoning_effort"] = _options.ReasoningEffort;
        }

        httpRequest.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Compatible chat provider returned {(int)response.StatusCode}: {Limit(rawJson, 1000)}");
        }

        var responseText = ExtractChatText(rawJson);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("Compatible chat response did not contain choices[0].message.content.");
        }

        return new AiProviderResult(
            Key, DisplayName, ModelId, ReasoningMode, startedAt, DateTimeOffset.UtcNow,
            responseText, false, Array.Empty<string>(), rawJson);
    }

    private void ApplyAuthorization(HttpRequestMessage request)
    {
        if (_options.AuthorizationHeaderName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                _options.AuthorizationScheme,
                _options.ApiKey);
            return;
        }

        var value = string.IsNullOrWhiteSpace(_options.AuthorizationScheme)
            ? _options.ApiKey
            : $"{_options.AuthorizationScheme} {_options.ApiKey}";
        request.Headers.TryAddWithoutValidation(_options.AuthorizationHeaderName, value);
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
