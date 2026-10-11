# Local Ollama agent

Mission 3 connects Ollama to the read-only tools from Mission 2. MealApp controls execution; the model only requests tools. Mission 4 adds an Admin-only recipe proposal tool with separate human approval; see [AGENT_APPROVALS.md](AGENT_APPROVALS.md). No model can directly publish recipes.

## Flow

`POST /api/agent/chat` -> existing User/Admin JWT authorization -> AgentChatService -> Ollama `/api/chat` -> model tool request -> existing AgentToolExecutor -> registered tool validation and trusted request identity -> read-only DTO result -> Ollama tool message -> final answer.

Each HTTP chat starts a fresh history. System instructions, the user's message, assistant messages and tool results have distinct roles. Tool results remain untrusted data. The model never receives the JWT or the authenticated user's email, ID, name or role. It receives only registry schemas, chat messages and public recipe/ingredient DTO data.

Function definitions sent to Ollama are derived from `ToolRegistry.Definitions`: names, descriptions and input JSON schemas. User requests expose the four READ tools; Admin requests also expose the WRITE proposal tool under C# policy. There is no independent hardcoded model tool list. Tool calls always go through `AgentToolExecutor`; the agent service has no DbContext access. Unknown tools, extra identity arguments, invalid limits and invalid recipe IDs remain subject to Mission 2's validation.

Ollama's supported wire format is documented in [Chat API](https://docs.ollama.com/api/chat) and [tool calling](https://docs.ollama.com/capabilities/tool-calling). The client uses non-streaming chat, structured function calls and `role: tool` with `tool_name` for results. It requests temperature zero and a maximum of 1024 output tokens per model turn. This reduces variability but does not guarantee identical natural-language answers.

## Configuration

Base `appsettings.json` disables the agent by default. Development enables it and supplies the installed local model and base URL. Production is unaffected unless the feature is explicitly enabled through trusted configuration; a disabled agent returns a controlled 503. Existing backend startup does not depend on Ollama availability.

```json
{
  "Agent": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "qwen3:4b-instruct",
    "BaseUrl": "http://localhost:11434",
    "MaxToolCalls": 5,
    "ModelTimeoutSeconds": 120,
    "RequestTimeoutSeconds": 180
  }
}
```

Equivalent environment variables are `Agent__Enabled`, `Agent__Provider`, `Agent__Model`, `Agent__BaseUrl`, `Agent__MaxToolCalls`, `Agent__ModelTimeoutSeconds`, and `Agent__RequestTimeoutSeconds`. There are no model credentials in source.

The configured base URL must be an HTTP/HTTPS loopback origin without credentials, query, fragment or a non-root path. It is never supplied by the user or model. Automatic redirects are disabled. This mission supports a local Ollama server on the backend's machine only. Inside a deployed container, localhost refers to that container; this configuration does not connect Render to your laptop.

MaxToolCalls must be 1-5, model timeout 1-300 seconds, and total request timeout 1-600 seconds. Invalid agent configuration returns a controlled 503 on chat rather than breaking existing application startup.

## Bounded execution and errors

At most five requested tools execute per HTTP request. Parallel tool requests count individually and execute sequentially using the same request-scoped DbContext. A batch exceeding the remaining budget is rejected before that batch executes. After the fifth tool, one further model turn may return the final answer; any further tool request stops with HTTP 422. No open-ended execution is possible. An overall deadline also bounds the request.

Any rejected tool or tool failure stops the request and returns a safe error with the executed tool trace. Successful tool results are fed back to the model. Failed results are not followed by further model calls, preventing retries that attempt to bypass validation or permissions. Tool errors retain their useful validation messages; internal tool failures become 503. Missing recipes produce 404.

Unavailable Ollama/provider HTTP errors return 503. A per-model or overall timeout returns 504. Malformed or empty model responses return 502. Unexpected application errors return 500 with a generic message. HTTP cancellation is propagated to the HTTP client and tool queries; cancelled requests are audited. Caller disconnection generally cannot receive a response. Ollama responses are bounded to 1 MiB, the final answer to 16000 characters, and the user message to 2000 characters. The incoming HTTP body is bounded to 16 KiB.

`ApiResponse.result` contains `answer` and `toolCalls`; each trace entry contains only registered tool name (or `<unknown>`), `isSuccess`, and a safe result `code`. Failed requests have an empty answer, any completed trace entries, and messages in `errorMessages`. No prompts, tokens, raw arguments or exceptions appear in diagnostic trace fields.

## Local manual test

1. Install/start Ollama. In a terminal, run `ollama serve` if it is not already running. On Windows the Ollama desktop application can also run the server.
2. Run `ollama pull qwen3:4b-instruct` if that model is not already installed. Confirm with `ollama list`. The official [Qwen3 tags](https://ollama.com/library/qwen3/tags) include this instruct model.
3. From `backend`, start the app using the existing Development configuration:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\MealApp.csproj --urls http://localhost:5280
```

4. Log in through `/api/auth/login` with your existing account. Copy `result.token` into Swagger's Bearer authorization or a local client variable. Do not put a real token in source files or screenshots.
5. Call `POST /api/agent/chat` with `Content-Type: application/json` and `Authorization: Bearer <token>`:

```json
{"message":"Find recipes containing chicken."}
```

Expected trace: `search_recipes` -> existing executor -> database results -> model answer. The precise text depends on your actual recipes.

6. Use an existing recipe ID (example 1):

```json
{"message":"What ingredients are required for recipe 1?"}
```

Expected trace: `get_recipe_ingredients` or `get_recipe` -> existing executor -> model answer. A nonexistent ID returns a safe 404.

Example success shape (illustrative contents):

```json
{
  "isSuccess": true,
  "result": {
    "answer": "Chicken Dinner serves four and uses 500 g of chicken.",
    "toolCalls": [{"tool":"get_recipe","isSuccess":true,"code":"success"}]
  },
  "errorMessages": [],
  "statusCode": 200
}
```

7. Stop Ollama and repeat a request to verify the safe connection error. Existing Recipe, MealPlan, ShoppingList, Support, auth and import endpoints continue operating normally.

## Audit and tests

Agent logs distinguish `started`, `model_requested_tool`, `tool_result`, `final_response`, `completed`, `failed`, and `tool_limit_reached`. They include trusted user ID, UTC timestamp, safe code/tool name and duration. Mission 2's executor continues its own validation/execution audit. Full prompts, model response bodies, JWTs and tool arguments are not logged.

Normal automated tests replace `IAgentModelClient` with a fake client or use an HTTP message handler fake. They never call a running Ollama instance. Run the complete suite:

```powershell
dotnet test .\backend\MealApp.Tests\MealApp.Tests.csproj /p:UseAppHost=false /p:UseSharedCompilation=false -o .\backend\test-build-output
```

A separate live smoke test can use the real model with an isolated EF InMemory fixture. Such a test proves model integration and trusted execution without touching development or production database records. Live results are documented below when verified.

## Verified live conversation

On 2026-10-04, the installed `qwen3:4b-instruct` model was exercised through the actual authenticated `/api/agent/chat` controller using a temporary test host. The database was an isolated EF InMemory fixture, not the existing MealApp development or production database. A test User JWT was issued through the existing login endpoint. No fake model was used for these two requests.

User: `Find recipes containing chicken.`

Tool trace: `search_recipes`, `isSuccess: true`, `code: success`.

Actual answer:

> I found a recipe containing chicken:
>
> - **Name**: Chicken Dinner
> - **Category**: Dinner
> - **Description**: Chicken with carrots
> - **Preparation Time**: 10 minutes
> - **Cooking Time**: 20 minutes
>
> Would you like more details about this recipe?

User: `What ingredients are required for recipe 1?`

Tool trace: `get_recipe_ingredients`, `isSuccess: true`, `code: success`.

Actual answer:

> The ingredients required for recipe 1 are:
>
> - Chicken: 500 grams (g)

Both responses were HTTP 200 with `ApiResponse.isSuccess: true`. After both requests the fixture still contained exactly one Recipe, one RecipeIngredient and one Ingredient. This confirms live model tool calling and response integration; it does not claim the fixture represents the user's real recipes.

## Limitations kept for later

There is no conversation persistence, streaming, frontend chat, durable audit store, new rate limiter, cloud model or remote Ollama host. Write capability is limited to Admin recipe proposals requiring separate human approval. Model text can still be inaccurate or follow malicious database text; role separation and the prompt reduce confusion, while the trusted executor prevents unauthorized actions. Returned natural-language answers should be treated as untrusted text. SQL-specific behavior uses the same existing read services and has not been tested against a live SQL server by these tests.
