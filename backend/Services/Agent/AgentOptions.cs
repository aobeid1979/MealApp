namespace MealApp.Services.Agent;

public class AgentOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Ollama";
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public int MaxToolCalls { get; set; } = 5;
    public int ModelTimeoutSeconds { get; set; } = 120;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public int MaxHistoryMessages { get; set; } = 20;
    public int MaxHistoryCharacters { get; set; } = 32000;

    public bool IsValid() => Provider == "Ollama" && !string.IsNullOrWhiteSpace(Model) && Model.Length <= 200
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0
        && MaxToolCalls is >= 1 and <= 5 && ModelTimeoutSeconds is >= 1 and <= 300
        && RequestTimeoutSeconds is >= 1 and <= 600
        && MaxHistoryMessages is >= 2 and <= 40 && MaxHistoryMessages % 2 == 0
        && MaxHistoryCharacters is >= 1000 and <= 64000;
}
