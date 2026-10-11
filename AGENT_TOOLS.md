# Read-only agent tools

MealApp owns the registry, argument validation, authorization and execution. These READ tools work without an LLM. Mission 4 adds one separately classified WRITE proposal tool; see [AGENT_APPROVALS.md](AGENT_APPROVALS.md). It cannot directly publish a Recipe.

## Architecture and endpoint

`POST /api/agent-tools/execute` accepts JSON and requires the existing JWT with User or Admin role. It is an authenticated application endpoint available for manual testing in any environment; it is not an anonymous debug endpoint.

The flow is authenticated request -> registry lookup -> strict schema validation -> explicit executor switch -> application read service/DbContext projection -> ApiResponse. Authentication is checked again inside the executor, which obtains the principal through `IHttpContextAccessor`; callers cannot pass a principal or identity in arguments.

`ToolRegistry.Definitions` exposes immutable definitions to application code for discovery. The four READ entries below remain, alongside the WRITE `create_recipe_draft` proposal tool. Adding a future tool requires an explicit registry definition, policy and executor branch. There is no reflection or dynamic method invocation.

Recipe reads were previously in RecipeController. `RecipeReadService` now shares recipe search and details queries with that controller while preserving its routes and response DTOs. The tool layer projects minimal summaries and strips internal relationship fields from recipe steps/tools. Ingredient quantities are stored values, not scaled servings or shopping-list calculations. Mission 1 imports are unchanged.

## Input schemas

All schemas use `type: object` and `additionalProperties: false`. Names are exact and case-sensitive. Unknown properties, duplicate properties, null arguments, wrong types, missing required fields and out-of-range values are rejected before database queries. The outer request also rejects unknown properties.

| Tool | Arguments | Result |
| --- | --- | --- |
| `search_recipes` | optional `query`, optional `category` (recipe category name), optional `limit` | Recipe summaries |
| `get_recipe` | required `recipeId` | Complete recipe DTO |
| `list_ingredients` | optional `query`, optional `category` (ingredient category name), optional `limit` | Ingredient summaries |
| `get_recipe_ingredients` | required `recipeId` | Ingredient/quantity/unit DTOs |

Strings must be at most 200 characters; leading/trailing whitespace is trimmed. Empty strings mean no filter. Limits are whole integers from 1 to 50, default 10. Recipe IDs are whole integers from 1 to 2147483647. Search matches recipe title/description or ingredient name case-insensitively. Category filters match the full existing name case-insensitively. Unknown categories produce an empty result. The SQL database collation controls culture/accent behavior.

Recipe searches sort by title then ID. Ingredient searches sort by name then ID. Recipe ingredients sort by name then row ID, steps by step number then ID, and tools by name then ID. This makes repeated reads deterministic for unchanged data.

HTTP request bodies are limited to 16 KiB. READ queries use no-tracking projections. The WRITE proposal branch stores a validated in-memory intent; only separate Admin approval endpoints can publish it. Existing recipe contents are returned in full; this mission does not add pagination or truncation to a single recipe.

## Responses and errors

Responses use the existing `ApiResponse`. `result` contains `tool`, `code`, and `data`. HTTP 200 means success (including an empty search array). A missing recipe returns HTTP 404, `code: not_found`, null data, and `Recipe not found.` in `errorMessages`. Unknown tools and invalid arguments return HTTP 400. Unauthenticated requests return 401; missing allowed roles return 403. Unexpected execution failures return 500 with a safe message. No raw exception is returned. Malformed HTTP JSON is handled by the existing standard validation response.

All examples below are illustrative; IDs and contents depend on the current database. Set `Content-Type: application/json` and `Authorization: Bearer <existing-login-token>`.

### search_recipes

Request:

```json
{"tool":"search_recipes","arguments":{"query":"chicken","category":"Dinner","limit":5}}
```

Response:

```json
{"isSuccess":true,"result":{"tool":"search_recipes","code":"success","data":[{"id":1,"name":"Roast Chicken","category":"Dinner","description":"Chicken with carrots.","preparationTimeMinutes":10,"cookingTimeMinutes":30}]},"errorMessages":[],"statusCode":200}
```

### get_recipe

Request:

```json
{"tool":"get_recipe","arguments":{"recipeId":1}}
```

Response:

```json
{"isSuccess":true,"result":{"tool":"get_recipe","code":"success","data":{"id":1,"name":"Roast Chicken","description":"Chicken with carrots.","category":"Dinner","servings":4,"preparationTimeMinutes":10,"cookingTimeMinutes":30,"ingredients":[{"ingredientId":2,"ingredientName":"Chicken","quantity":500,"unitId":1,"unitName":"Gram","unitShortName":"g"}],"steps":[{"stepNumber":1,"instruction":"Roast until safely cooked."}],"tools":["Roasting pan"]}},"errorMessages":[],"statusCode":200}
```

### list_ingredients

Request:

```json
{"tool":"list_ingredients","arguments":{"query":"car","category":"Vegetables","limit":10}}
```

Response:

```json
{"isSuccess":true,"result":{"tool":"list_ingredients","code":"success","data":[{"id":3,"name":"Carrot","ingredientCategory":"Vegetables"}]},"errorMessages":[],"statusCode":200}
```

### get_recipe_ingredients

Request:

```json
{"tool":"get_recipe_ingredients","arguments":{"recipeId":1}}
```

Response:

```json
{"isSuccess":true,"result":{"tool":"get_recipe_ingredients","code":"success","data":[{"ingredientId":2,"ingredientName":"Chicken","quantity":500,"unitId":1,"unitName":"Gram","unitShortName":"g"}]},"errorMessages":[],"statusCode":200}
```

Missing recipe example:

```json
{"isSuccess":false,"result":{"tool":"get_recipe","code":"not_found","data":null},"errorMessages":["Recipe not found."],"statusCode":404}
```

## Audit and limitations

Each executor invocation writes an Information-level structured log containing the registered tool name (or `<unknown>`), trusted authenticated user ID when available, UTC timestamp, success/failure code, validation failure flag and elapsed milliseconds. Tokens, passwords, raw arguments and returned data are not logged. Unexpected errors have a separate backend technical exception log. HTTP failures rejected by authentication/model binding happen before tool execution and use existing middleware behavior.

Audit logs are ordinary application logs, not a durable database audit ledger. There are no write tools, rate-limit changes, approvals, agent sessions or model integration. Tests use the existing EF InMemory integration-test factory, so SQL Server-specific collation/translation behavior is not verified against a live database. Existing CRUD, seeders and imports remain responsible for their own data integrity. Search result ordering is deterministic for a stable database; it does not provide a multi-request snapshot during concurrent updates.
