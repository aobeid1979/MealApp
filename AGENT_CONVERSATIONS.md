# Mission 6: Persistent agent conversations

## Scope and storage

Conversation metadata and completed chat text are stored in the application's SQL database, not browser storage. Existing Identity users own their conversations. No recipe, shopping-list, authentication or approval data is migrated.

Migration: `20261011072407_AddAgentConversations`. Apply with the existing local connection configuration:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project backend/MealApp.csproj
```

Review the target database before running this command. This mission does not apply migrations to Azure/production automatically.

### Tables

- `AgentConversations`: GUID ID, Identity UserId, Title (100 characters), CreatedAtUtc/UpdatedAtUtc (`datetimeoffset`), NextSequence, ActiveTurnId and an optimistic-concurrency Revision GUID. Index `(UserId, UpdatedAtUtc)` supports the private most-recent-first list.
- `AgentMessages`: GUID ID, ConversationId, TurnId, Sequence, Role (`User` or `Assistant`), Content (application maximum 16,000 characters), CreatedAtUtc. Unique `(ConversationId, Sequence)` gives deterministic ordering; TurnId has a lookup index. User input is further limited to 2,000 characters.
- `AgentConversationTurns`: durable idempotency receipt with server-generated ID, ConversationId, client RequestId, SHA-256 of trimmed input, Status (`Processing`, `Completed`, `Failed`) and CreatedAtUtc. Unique `(ConversationId, RequestId)` prevents duplicate receipts.

The conversation foreign keys cascade to its messages and receipts. Conversation deletion does not delete users, recipes or proposed actions. TurnId on messages is an application correlation value, not another cascade foreign key, avoiding SQL Server multiple cascade paths. All writes use UTC timestamps.

Only user text and final assistant text become messages. JWTs, system prompts, tool arguments/results, pending-action objects and approval secrets are not copied into message storage. User-entered text itself is stored as supplied (trimmed), so users should not paste secrets into chat. Application audit messages do not log chat content or exception payloads.

## API

All routes require a valid JWT with User or Admin role and use the existing `ApiResponse` envelope.

| Method | Route | Result |
| --- | --- | --- |
| GET | `/api/agent/conversations?page=1&pageSize=20` | `{items,page,hasMore}`; pageSize 1-50, page 1-10000 |
| POST | `/api/agent/conversations` | Conversation summary; empty body or `{}` |
| GET | `/api/agent/conversations/{id}?pageSize=50&beforeSequence=101` | `{conversation,messages,hasMore,nextBeforeSequence}`; latest page in chronological order, pageSize 1-100 |
| DELETE | `/api/agent/conversations/{id}` | `{message:"Conversation deleted."}` |
| POST | `/api/agent/chat` | Existing chat reply plus conversationId/requestId for persistent turns |

```json
{
  "conversationId": "existing-owned-conversation-guid",
  "requestId": "new-client-generated-guid-for-this-attempt",
  "message": "What ingredients does it need?"
}
```

ConversationId and RequestId must be supplied together. RequestId is an idempotency key, not identity or permission. Unknown JSON fields, including client history and UserId, are rejected. The legacy `{message}` chat request remains stateless and compatible; `/assistant` now always uses persistent turns.

Every conversation lookup filters by the authenticated Identity ID. Other users, including Admins, receive the same safe 404 as nonexistent conversations. The API never accepts an owner from the frontend or model. Pagination is bounded; messages are rendered as text, not HTML.

## Context

The server selects the latest complete successful user/assistant pairs in the selected owned conversation, ordered by Sequence. Defaults:

```text
Agent__MaxHistoryMessages=20
Agent__MaxHistoryCharacters=32000
```

Message limit must be even and between 2 and 40. Character budget must be 1000-64000. Limits exclude the system prompt and current input; existing current-turn tool/time limits remain. Selection stops before a whole pair would exceed the character budget; partial turns are never supplied. Older messages remain stored and paginated, but are not unlimited model context.

The unchanged trusted system/tool policies are followed by text-only historical `user`/`assistant` messages and the new user input. Historical tool calls are not stored or replayed. The system prompt explicitly marks history as untrusted context, not instructions, authorization or proof of execution. Trusted tools must recheck database facts and permissions. An old approval claim cannot authorize a write.

The prompt also asks for actual public recipe IDs alongside recipe names, and a fresh search when a referenced ID is unavailable. This supports follow-ups without persisting raw tool results or guessing IDs. It is model guidance, not an authorization rule.

## Persistence and retries

1. Validate ownership, input and existing receipt.
2. Reserve the turn with one short atomic SaveChanges and update the conversation concurrency revision. A conversation permits only one active turn.
3. Dispose that database context before calling the existing agent orchestration. No database transaction is held while waiting for Ollama.
4. On success, use a separate fresh context to atomically save both messages, allocate consecutive sequence values, update metadata and complete the receipt.

The first completed user message supplies a deterministic whitespace-normalized title, truncated at 100 characters, without another model call.

- A repeated completed RequestId returns its saved answer without calling the model/tools again. Historical activity and pending-action objects are deliberately not reconstructed.
- A RequestId reused for different trimmed input returns 409.
- A concurrent or uncertain `Processing` turn returns 409; it is never automatically rerun.
- Model unavailable/timeouts/tool failures return existing safe errors, save no successful messages, and mark the receipt Failed. Retrying that receipt returns 409 with `result.requestState="Failed"`. The UI requires an explicit **Start a new attempt** before issuing a new RequestId.
- Caller cancellation during orchestration attempts to mark the receipt Failed with an independent bounded cleanup token. Cancellation during the final database write instead preserves commit uncertainty; retry the same RequestId, never automatically rerun it.
- If final persistence fails, return safe 503 and keep the reservation when commit status is uncertain. Retry the SAME RequestId: it either returns the committed answer or safely blocks; never rerun a possible write tool automatically.
- Concurrent deletion wins safely: a late reply cannot recreate deleted messages.

There is no automatic recovery/replay of crashed Processing turns. An interrupted conversation may stay reserved; the user can delete it and start another. Before retrying any possible Admin proposal as a new turn, check the authoritative approval list. This conservative limitation avoids duplicate proposal execution. Exactly-once behavior does not extend across intentionally different RequestIds or deleted/recreated conversations.

## Frontend and approval

`/assistant` has New Chat, a paginated private list, selected title, paginated message history, Send, and confirmed Delete. The selected ID is in the URL, so refresh restores that conversation. Messages are fetched again after a successful send, never optimistically duplicated. A failed/uncertain attempt retains its input and RequestId for same-attempt retries while mounted. Request IDs use Web Crypto with a v4 UUID fallback for plain-HTTP local-network development origins where randomUUID is unavailable.

The existing sessionStorage/JWT helpers remain. Session-change notifications remount agent descendants; logout, expiry and account switches discard in-memory history and abort outstanding requests. Responses are checked against the current token before being used. Conversation fetches and backend agent responses explicitly disable HTTP caching with `no-store`. No conversation content is cached in local/session storage.

New live replies keep the safe Agent Activity summaries. Saved/retried replies contain timestamped historical text, not a live pending badge. Admins can use **Review proposal status** on a live reply or the existing **Agent proposals** navigation link to consult the authoritative actions UI. Stored text describing a proposal is historical, not evidence that it remains pending or was published. Approval/rejection and Admin-only proposal policies are unchanged. Pending actions still use Mission 4's in-memory store and do not become durable just because the conversation is durable.

## Verification

```powershell
dotnet test backend/MealApp.Tests/MealApp.Tests.csproj --no-restore /p:UseAppHost=false -o backend/test-build-output
cd frontend
node --test tests/*.test.cjs
npm run lint
npm run build
```

Backend tests cover persistence/relogin/deletion, chronology/pagination, owner isolation including Admin, client identity/history rejection, bounded complete-pair context, no historical tool replay, model unavailable/timeouts, persistence failure, concurrent turns, duplicate retries and persistent-context approval boundaries. Existing Missions 1-5 tests remain regression coverage. Integration tests use disposable EF InMemory databases; they do not prove SQL Server locking/transaction behavior. Review/apply the generated SQL migration separately against a disposable/local SQL database before deployment.

Frontend tests cover API response shapes, failed-attempt detection and auth privacy events. Browser smoke verification details/results are recorded with this mission's completion report.

### Mission 6 verification results

- Backend suite: 167 passed, including 15 conversation cases and all existing 152 regression cases.
- Frontend native Node tests: 11 passed; ESLint and production build passed.
- SQL Server model/snapshot consistency and generated CREATE SQL checked without opening a database connection. Migration was created, not applied to existing databases.
- Real Qwen (`qwen3:4b-instruct`) used through the actual ASP.NET app in a disposable test host. Only EF database/support-email services were substituted; the model client, controllers, JWT, tool execution, persistence service and approval workflow were real.
- Browser passed: login, New Chat, chicken search, follow-up ingredient read (500 g Chicken), reload, logout/relogin/continue, second-user private list and direct foreign-ID 404, confirmed delete and subsequent 404, Admin draft unpublished until approval, publication through the existing Admin UI, and no uncaught browser errors.
- A separate browser check passed: mobile framing/no horizontal overflow, no stale live proposal badge after reload, mounted account switch discards transcript, logout redirects.
- The CPU-only Qwen test host temporarily used 300-second model/600-second request limits and a longer proxy timeout. Application defaults remain unchanged. Initial model runs guessed a missing recipe ID; explicit ID/reference guidance was added and the final real-model scenario passed. Natural-language/tool-choice reliability remains model-dependent.
- One existing timer-based model-client cancellation test raced under concurrent CPU-heavy work; the full suite passed when rerun without competing build/model jobs. No model-client or cancellation behavior was changed.
- Disposable users, recipes and conversations existed only in the test host's EF InMemory store. No development/production database records were modified. This browser test does not substitute for applying/testing the migration against SQL Server.

### Changed files

Added:

```text
AGENT_CONVERSATIONS.md
backend/Models/AgentConversation.cs
backend/Models/AgentMessage.cs
backend/Models/AgentConversationTurn.cs
backend/DTOs/Agent/ConversationDtos.cs
backend/Services/Agent/AgentConversationService.cs
backend/Controllers/AgentConversationsController.cs
backend/Migrations/20261011072407_AddAgentConversations.cs
backend/Migrations/20261011072407_AddAgentConversations.Designer.cs
backend/MealApp.Tests/AgentConversationTests.cs
frontend/src/components/AgentConversations.tsx
frontend/src/lib/agent-request.ts
frontend/tests/agent-request.test.cjs
frontend/tests/agent-session.test.cjs
```

Modified:

```text
AGENT_UI.md
LOCAL_AGENT.md
backend/Controllers/AgentController.cs
backend/DTOs/Agent/AgentDtos.cs
backend/Data/ApplicationDbContext.cs
backend/Migrations/ApplicationDbContextModelSnapshot.cs
backend/Program.cs
backend/Services/Agent/AgentChatService.cs
backend/Services/Agent/AgentOptions.cs
frontend/src/app/assistant/page.tsx
frontend/src/components/AgentAccess.tsx
frontend/src/lib/agent-response.ts
frontend/src/lib/auth.ts
frontend/tests/agent-response.test.cjs
```

### Manual flow

1. Apply the migration to the intended **local** database; start Ollama with the configured Qwen model, backend and frontend using the existing development configuration.
2. Login as User. Open `/assistant`, New Chat, ask `Find me a chicken recipe.` Then ask `What ingredients does it need?` Confirm the reference and current read-tool result.
3. Reload; confirm the same messages remain. Logout/login as the same user; reopen that conversation and continue.
4. Login as another user (also test another Admin); confirm the first user's list is absent and direct requests for its ID return 404.
5. Login as Admin, propose a valid recipe using existing ingredients/category/units. Confirm no publication before approval. Review and approve through `/admin/agent-actions`; confirm publication exactly once. Reject a second proposal; confirm no publication.
6. Confirm deleting a conversation removes it and its messages; GET that ID returns 404. Test mobile width, empty list, failed connection and duplicate clicks.

## Limitations

- No streaming, embeddings, semantic memory, retention scheduler or automatic stuck-turn recovery.
- The latest bounded text may omit the subject of a very old follow-up; ask again or use a new conversation.
- A browser reload loses an in-flight client nonce; the durable backend reservation still prevents automatic replay and may require another conversation.
- Private conversation text remains in normal SQL backups according to the database operator's retention policy after API deletion.
- Apply this migration before enabling the new frontend against an existing database. No production database was changed by this implementation.
