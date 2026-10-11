using System.Net.Http.Json;
using System.Text.Json;
using MealApp.Services.AgentTools;
using Microsoft.Extensions.Options;

namespace MealApp.Services.Agent;

public class OllamaModelClient(HttpClient httpClient, IOptions<AgentOptions> options) : IAgentModelClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumResponseBytes = 1024 * 1024;

    public async Task<ModelMessage> ChatAsync(IReadOnlyList<ModelMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.IsValid())
            throw new AgentModelException("not_configured", "The local assistant is not configured or enabled.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ModelTimeoutSeconds));
        try
        {
            var payload = new
            {
                model = settings.Model, messages, stream = false,
                tools = tools.Select(x => new { type = "function", function = new { name = x.Name, description = x.Description, parameters = x.InputSchema } }),
                options = new { temperature = 0, num_predict = 1024 }
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "api/chat"))
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new AgentModelException("model_unavailable", "The local model is unavailable. Check Ollama and the configured model.");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes) throw Malformed();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes, timeout.Token)) > 0)
            {
                if (buffer.Length + count > MaximumResponseBytes) throw Malformed();
                await buffer.WriteAsync(bytes.AsMemory(0, count), timeout.Token);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True
                || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String || role.GetString() != "assistant")
                throw Malformed();
            var content = "";
            if (message.TryGetProperty("content", out var text))
            {
                if (text.ValueKind != JsonValueKind.String) throw Malformed();
                content = text.GetString()!;
            }
            var calls = new List<ModelToolCall>();
            if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind != JsonValueKind.Null)
            {
                if (toolCalls.ValueKind != JsonValueKind.Array || toolCalls.GetArrayLength() > 5) throw Malformed();
                foreach (var call in toolCalls.EnumerateArray())
                {
                    if (call.ValueKind != JsonValueKind.Object || !call.TryGetProperty("function", out var function) || function.ValueKind != JsonValueKind.Object
                        || !function.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(name.GetString())
                        || name.GetString()!.Length > 100 || !function.TryGetProperty("arguments", out var arguments))
                        throw Malformed();
                    // Argument semantics remain the trusted tool executor's responsibility.
                    calls.Add(new(new(name.GetString()!, arguments.Clone())));
                }
            }
            if (content.Length > 16000) throw Malformed();
            return new ModelMessage("assistant", content) { ToolCalls = calls.Count > 0 ? calls : null };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AgentModelException("model_timeout", "The local model took too long to respond. Please try again.");
        }
        catch (HttpRequestException)
        {
            throw new AgentModelException("model_unavailable", "Unable to connect to Ollama. Start the local model server and try again.");
        }
        catch (JsonException) { throw Malformed(); }
    }

    private static AgentModelException Malformed() => new("malformed_model_response", "The local model returned an invalid response. Please try again.");
}
