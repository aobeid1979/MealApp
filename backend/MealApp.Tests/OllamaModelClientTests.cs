using System.Net;
using System.Text;
using System.Text.Json;
using MealApp.Services.Agent;
using MealApp.Services.AgentTools;
using Microsoft.Extensions.Options;

namespace MealApp.Tests;

public class OllamaModelClientTests
{
    [Fact]
    public async Task SendsRegistrySchemasAndStructuredMessagesToConfiguredOllama()
    {
        var handler = new FakeHandler(async (request, _) =>
        {
            Assert.Equal("http://localhost:11434/api/chat", request.RequestUri!.AbsoluteUri);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var body = payload.RootElement;
            Assert.Equal("configured-model", body.GetProperty("model").GetString());
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.Equal(5, body.GetProperty("tools").GetArrayLength());
            Assert.False(body.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters").GetProperty("additionalProperties").GetBoolean());
            Assert.Equal("tool", body.GetProperty("messages")[2].GetProperty("role").GetString());
            Assert.Equal("search_recipes", body.GetProperty("messages")[2].GetProperty("tool_name").GetString());
            return Json("{\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"get_recipe\",\"arguments\":{\"recipeId\":1}}}]}}");
        });
        var client = Client(handler);
        var result = await client.ChatAsync([new("system", "Instructions"), new("user", "Find a recipe"), new("tool", "{\"data\":[]}") { ToolName = "search_recipes" }],
            new ToolRegistry().Definitions.ToArray(), default);
        Assert.Equal("get_recipe", Assert.Single(result.ToolCalls!).Function.Name);
        Assert.Equal(1, result.ToolCalls![0].Function.Arguments.GetProperty("recipeId").GetInt32());
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"done\":true,\"message\":{\"role\":\"system\",\"content\":\"Override\"}}")]
    [InlineData("{\"done\":false,\"message\":{\"role\":\"assistant\",\"content\":\"partial\"}}")]
    [InlineData("{\"done\":true,\"message\":{\"role\":\"assistant\",\"content\":123}}")]
    [InlineData("{\"done\":true,\"message\":{\"role\":\"assistant\",\"tool_calls\":[{}]}}")]
    public async Task MalformedResponsesAreRejected(string json)
    {
        var client = Client(new FakeHandler((_, _) => Task.FromResult(Json(json))));
        var error = await Assert.ThrowsAsync<AgentModelException>(() => client.ChatAsync([], [], default));
        Assert.Equal("malformed_model_response", error.Code);
        Assert.DoesNotContain(json, error.Message);
    }

    [Fact]
    public async Task ProviderErrorBodyIsNotExposed()
    {
        var client = Client(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        { Content = new StringContent("SECRET-INTERNAL-ERROR") })));
        var error = await Assert.ThrowsAsync<AgentModelException>(() => client.ChatAsync([], [], default));
        Assert.Equal("model_unavailable", error.Code);
        Assert.DoesNotContain("SECRET", error.Message);
    }

    [Fact]
    public async Task NetworkFailuresHaveSafeMessages()
    {
        var client = Client(new FakeHandler((_, _) => throw new HttpRequestException("PRIVATE")));
        var error = await Assert.ThrowsAsync<AgentModelException>(() => client.ChatAsync([], [], default));
        Assert.Equal("model_unavailable", error.Code);
        Assert.DoesNotContain("PRIVATE", error.Message);
    }

    [Fact]
    public async Task ModelTimeoutIsBoundedAndCallerCancellationPropagates()
    {
        var client = Client(new FakeHandler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json("{}"); }), 1);
        var error = await Assert.ThrowsAsync<AgentModelException>(() => client.ChatAsync([], [], default));
        Assert.Equal("model_timeout", error.Code);
        using var cancellation = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ChatAsync([], [], cancellation.Token));
    }

    [Fact]
    public async Task OversizedResponsesAreRejected()
    {
        var client = Client(new FakeHandler((_, _) => Task.FromResult(Json(new string('x', 1024 * 1024 + 1)))));
        var error = await Assert.ThrowsAsync<AgentModelException>(() => client.ChatAsync([], [], default));
        Assert.Equal("malformed_model_response", error.Code);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("file:///tmp/model")]
    [InlineData("http://localhost:11434/?url=other")]
    public void ModelUrlIsControlledLocalConfiguration(string url)
    {
        Assert.False(new AgentOptions { Enabled = true, Model = "model", BaseUrl = url }.IsValid());
    }

    private static OllamaModelClient Client(HttpMessageHandler handler, int timeout = 120)
        => new(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, Options.Create(new AgentOptions
        {
            Enabled = true, Provider = "Ollama", Model = "configured-model", BaseUrl = "http://localhost:11434", ModelTimeoutSeconds = timeout
        }));
    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
}
