namespace AiTrustCalibration.Api.Ai;

public sealed class AiOptions
{
    public string ResearchInstruction { get; set; } =
        "Ответь на профессиональную задачу максимально точно. Не выдумывай отсутствующие факты. " +
        "Если данных недостаточно для уверенного вывода, явно укажи, каких данных не хватает. " +
        "Не утверждай, что выполнил действия или проверки, которых фактически не выполнял.";

    public ProviderOptions OpenAI { get; set; } = new();
    public DeepSeekOptions DeepSeek { get; set; } = new();
    public CompatibleChatOptions Yandex { get; set; } = new();
}

public class ProviderOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
}

public sealed class DeepSeekOptions : ProviderOptions
{
    public bool ThinkingEnabled { get; set; } = true;
}

public sealed class CompatibleChatOptions : ProviderOptions
{
    public string CompletionPath { get; set; } = "chat/completions";
    public string AuthorizationHeaderName { get; set; } = "Authorization";
    public string AuthorizationScheme { get; set; } = "Bearer";
}
