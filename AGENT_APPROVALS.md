# Controlled recipe proposals and human approval

Mission 4 adds exactly one WRITE capability: `create_recipe_draft`. The four existing READ tools retain their behavior. The model cannot publish recipes, approve actions, update/delete data, execute SQL or call arbitrary URLs. There are no migrations, new database tables, external SDKs or frontend chat changes.

## Architecture

`ToolDefinition.Capability` is trusted application metadata (`Read` or `Write`). `ReadOnly` is derived from it for compatibility. The deterministic C# `AgentToolPolicy` evaluates authenticated identity:

| Capability | Caller | Decision |
| --- | --- | --- |
| Registered READ tool | User/Admin | Allow |
| `create_recipe_draft` WRITE tool | Admin | RequireApproval |
| WRITE tool | User | Deny |
| Unknown or unimplemented write capability | Any | Deny/reject |

Tool schemas are still derived from ToolRegistry. Admin model requests receive the four READ tools plus the proposal tool. User requests receive only READ tools. A model that requests a hidden or unknown operation is still validated and denied by the trusted executor. There is no `approve_action` or `execute_action` tool.

The trusted flow is model proposal -> ToolRegistry recursive schema validation -> authenticated identity -> C# policy -> existing DataImportService recipe validation/normalization -> in-memory pending intent. This path never saves a Recipe graph.

Approval is separate: authenticated human Admin HTTP request -> current account/role check -> pending action lookup -> per-action lock -> verify Pending -> approve stored intent -> revalidate normalized payload and reference IDs -> existing DataImportService SQL transaction -> mark Executed. Ollama is never called during approval or rejection.

## Schema and deterministic validation

`create_recipe_draft` accepts this payload directly as `arguments`:

```json
{
  "name": "Simple Chicken Pasta",
  "description": "Pasta with chicken.",
  "category": "Pasta",
  "servings": 4,
  "preparationTimeMinutes": 15,
  "cookingTimeMinutes": 25,
  "ingredients": [
    {"ingredient":"Chicken Breast","quantity":500,"unit":"g"},
    {"ingredient":"Spaghetti","quantity":400,"unit":"g"}
  ],
  "steps": [
    {"stepNumber":1,"instruction":"Prepare the ingredients."},
    {"stepNumber":2,"instruction":"Cook chicken safely, boil pasta, and combine."}
  ],
  "tools": ["Knife", "Pan", "Pot"]
}
```

The actual JSON schema is in `ToolRegistry.DraftSchema()`. Required properties are name, category, servings, preparation/cooking times, ingredients and steps. Description, imageUrl and tools are optional. Unknown properties and duplicate JSON properties are rejected recursively, including nested ingredient IDs, userId, roles, approval state, policy decisions or execution status.

Names/category/reference names are limited to 200 characters, description/instructions to 5000, HTTP/HTTPS image URLs to 2048. Servings must be 1-50; times 0-10080 minutes. Ingredients and steps have 1-100 entries, tools 0-100. Quantities must be positive, fit decimal(18,2), and have at most two decimal places. Step numbers must be positive and unique, instructions nonempty, ingredients unique per recipe, and tools nonempty and unique case-insensitively. Steps are sorted by number and tools by name in the normalized intent.

The importer normalizes whitespace and resolves category, ingredient and unit names case-insensitively. A unit can use its name or short name. Unknown/ambiguous references are validation failures; no category, ingredient or unit is implicitly created. Existing recipe titles are rejected. All proposal validation uses Mission 1's existing graph-building rules; import endpoints preserve their insert/preview behavior.

## Pending intents and payload binding

`IPendingActionStore` isolates storage. The singleton in-memory implementation retains up to 1000 actions per process, including terminal actions. Each intent has a server-generated UUID, action type, trusted requesting user ID, UTC creation timestamp, status and RequireApproval decision.

The normalized payload is serialized immediately to an immutable JSON string. Resolved category ID and ingredient/unit IDs are stored as an immutable reference binding. A SHA-256 hash binds the action ID/type, requester, payload and references for review diagnostics. No model-supplied trusted fields are stored. The approval request cannot supply a new payload; empty body or `{}` is accepted, other properties are rejected.

Execution deserializes the stored payload, never a newly generated model response. It revalidates against current database data inside the existing import transaction and compares resolved IDs to the original binding. Deleting/replacing a referenced record with a different ID fails approval rather than silently targeting the replacement. Name changes, missing references, new ambiguities and new duplicate recipe titles also fail revalidation.

Statuses are Pending -> Rejected, or Pending -> Approved -> Executed/Failed. An action leaves Pending at most once. Failures after approval are terminal; there is no automatic replay or retry approval after an uncertain database commit. A new proposal requires a new explicit human review. EF's existing transient retry strategy still wraps the import transaction.

Both approver and original requester must still be Admins in Identity at approval time. A stale Admin JWT cannot restore a revoked database role. Any current Admin can review another Admin's proposal; requesting and deciding IDs remain recorded. User/anonymous access is blocked.

## HTTP endpoints and responses

All responses use ApiResponse.

- `POST /api/agent-tools/execute`: `create_recipe_draft` returns HTTP 200, `code: approval_required`, with actionId, Pending status and a message; User requests are denied.
- `POST /api/agent/chat`: Admins may request the draft through Ollama. Draft success stops the tool loop. One final tool-result notification goes to the model with no tools exposed; any follow-up model tool requests are never executed. The application provides authoritative approval-status text so a model cannot falsely claim publication. Failure of this notification does not undo or hide an already-created proposal.
- `GET /api/agent-actions/pending`: Admin-only list containing normalized payloads and review metadata.
- `POST /api/agent-actions/{actionId}/approve`: Admin-only; executes the stored intent immediately once.
- `POST /api/agent-actions/{actionId}/reject`: Admin-only; never writes Recipe data.

Example chat response:

```json
{
  "isSuccess": true,
  "result": {
    "answer": "Recipe proposal created and awaiting human Admin approval. It has not been published.",
    "toolCalls": [{"tool":"create_recipe_draft","isSuccess":true,"code":"approval_required"}],
    "pendingAction": {
      "actionId": "<server-generated UUID>",
      "status": "Pending",
      "message": "Recipe proposal created. Human Admin approval is required before publication."
    }
  },
  "errorMessages": [],
  "statusCode": 200
}
```

Approval/rejection returns the updated pending-action snapshot. Successful approval includes status Executed and recipeId. Unknown UUIDs return 404. Already decided actions, changed references and duplicate titles return 409. Invalid proposals/approval bodies return 400, unauthenticated requests 401, denied roles 403. Unexpected execution failures return a safe 500 with no raw exception in the response.

## Manual end-to-end scenario

1. Start Ollama with the installed `qwen3:4b-instruct` model and run MealApp in Development as described in LOCAL_AGENT.md.
2. Log in as an existing Admin. Use its JWT in Swagger or an HTTP client. Confirm the intended category, ingredients and units exist using the existing public APIs. Substitute real names if the sample names above do not exist.
3. Send `POST /api/agent/chat`:

```json
{"message":"Create a recipe draft called Simple Chicken Pasta using existing Chicken Breast and Spaghetti, category Pasta, units g, servings 4, preparation time 15 minutes and cooking time 25 minutes. Include preparation steps and cooking tools. Do not publish it."}
```

4. Expect `approval_required` and `result.pendingAction.actionId`. Verify `GET /api/recipe?search=Simple%20Chicken%20Pasta` still returns no such recipe. A validation failure means the proposal was not stored; correct the known reference names and resend.
5. Call `GET /api/agent-actions/pending` using the Admin JWT. Review the exact normalized payload, requester, action ID and hash.
6. Send `POST /api/agent-actions/{actionId}/approve` with no body or `{}`. No LLM participates. Expect Executed and a recipeId. Confirm through `GET /api/recipe/{recipeId}/details` or the normal search tool.
7. Repeat Approve or double-click it. Expect 409; verify only one recipe exists.
8. Propose a different unique title, review it, then POST `/reject`. Expect Rejected. Search must still find no recipe for that title. A later approve of that same action returns 409.
9. Log in as User and try listing, approving, rejecting or proposing. Expect denial; normal READ tools continue working.

## Audit and tests

Structured backend audit stages cover write tool requested, policy evaluated, pending action created, approval/rejection requested, approved, rejected, execution started/succeeded/failed, and repeated execution attempt rejected. Logs contain trusted user ID, action ID/type where available, UTC timestamp, policy and result. They do not log payloads, prompts, JWTs or secrets.

Normal tests use fake model clients and the existing isolated EF InMemory factory. They cover policy, recursive argument spoofing, proposal validation, exact normalized payload execution, role revocation, changed reference binding, rejection, repeated/concurrent approvals, application-controlled approval text, model attempts to approve, audit lifecycle, and all previous mission behavior. No running Ollama is required by the test suite.

## Verified live Qwen scenario

On 2026-10-04 the installed `qwen3:4b-instruct` model was tested through the real `/api/agent/chat` pipeline with an Admin JWT and an isolated EF InMemory fixture. The fixture started with one existing recipe and one ingredient. This did not touch the user's development or production database.

Actual request:

> Propose a new recipe called Simple Chicken Pasta. Use the existing category Dinner and existing ingredient Chicken with quantity 500 g, servings 4, preparation time 15 minutes and cooking time 25 minutes. Include a step to cook safely and the tool Pan. Use create_recipe_draft; do not publish or approve it.

Actual response: HTTP 200, tool `create_recipe_draft`, `isSuccess: true`, `code: approval_required`, and a server-generated Pending action ID.

The application's authoritative answer was:

> Recipe proposal created and awaiting human Admin approval. It has not been published.

The fixture still had one Recipe before approval. GET pending returned 200. A separate authenticated Admin HTTP approval request made by the smoke-test client returned 200 with Executed status. Repeating that approval returned 409. The fixture then had exactly two Recipes and two RecipeIngredients, while Ingredients stayed at one. No LLM took part in either approval request.

## Deliberate limitations

Pending actions and audit logs are not durable. Pending actions disappear on process restart; separate replicas do not share them. This implementation is for a single application process. The per-action semaphore prevents concurrent execution in that process, not a distributed approval protocol. At capacity, restart clears the store; there is no cleanup/expiry endpoint yet. Mission 5 adds the approval UI described in [AGENT_UI.md](AGENT_UI.md). There is no external-agent authentication, approval signing infrastructure or durable execution ledger.

A database commit and the in-memory status transition cannot be atomic. An uncertain failure remains terminal and must be reviewed manually. Tests verify concurrency and application behavior with EF InMemory; actual SQL transaction rollback/commit ambiguity is not fault-injected against Azure SQL. No development or production data is replaced or deleted by this mission.
