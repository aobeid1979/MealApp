# Development import examples

These files are manual development fixtures: 25 ingredients and 5 recipes. They are never read by startup seeders, EF migrations, or production startup. No production data is imported automatically.

Use an Admin JWT from the existing login endpoint. Send JSON with `Content-Type: application/json` and `Authorization: Bearer <token>`.

1. Ensure the referenced ingredient categories, recipe categories and units exist. The existing development sample seeder creates the categories and units used here; alternatively create them using their existing Admin APIs.
2. POST `ingredients.json` to `/api/import/ingredients/preview`.
3. Inspect `result.validationErrors` and `result.rows`, then POST the same JSON to `/api/import/ingredients`.
4. POST `recipes.json` to `/api/import/recipes/preview`, inspect the result, then POST it to `/api/import/recipes`.

References use existing category/ingredient names, compared case-insensitively after whitespace normalization. Units accept either an existing name or short name. Ambiguous references are rejected. No categories, units, or missing ingredients are created implicitly.

Ingredient records do not have a default unit in MealApp. Units belong to recipe ingredient quantities.

## Response semantics

Responses use MealApp's existing `ApiResponse`. A successfully processed batch returns HTTP 200 with an import report in `result`, even when some rows are invalid. Inspect the report before considering a batch fully imported. Malformed JSON, empty batches and batches exceeding 100 rows return HTTP 400. Unauthorized/non-Admin requests return 401/403.

- `totalReceived`: number of input rows.
- `ready`: valid, nonduplicate rows; includes imported rows during execution.
- `imported`: rows actually written; always zero for preview.
- `skipped`: invalid plus duplicate rows.
- `duplicates`: existing or earlier valid batch names, matched case-insensitively.
- `validationErrors`: one-based row, field and safe explanation.
- `rows`: normalized name, status (`ready`, `imported`, `duplicate`, `invalid`) and new ID when imported.

Preview never writes and does not issue a trusted approval token. Execution always normalizes, resolves references and validates again. Valid rows can be imported even if other rows are invalid. Each complete recipe graph is saved together. On SQL Server, an unexpected database failure rolls back the entire execution batch; the transaction participates in EF Core's existing transient retry strategy.

Inputs are bounded to 100 rows and a 2 MiB HTTP body. Recipe child collections are bounded to 100 each. Names are required and limited to 200 characters; descriptions/instructions to 5000; image URLs must be HTTP/HTTPS and at most 2048 characters. URLs are stored, never fetched by the importer. Recipes require 1-50 servings, nonnegative times of at most 10080 minutes, at least one ingredient and step, positive decimal(18,2) quantities, unique ingredients, positive unique step numbers, and unique nonempty tool names. Tools and images are optional.

No existing record is modified or deleted. Duplicate titles/names are skipped; this is an insert-only import, not an update or merge tool. Existing CRUD endpoints do not enforce database-level unique names; concurrent writes from other application paths remain an existing limitation. The import transaction uses serializable isolation to protect its own lookup/write sequence.
