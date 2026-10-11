# Mission 5: Assistant and human approval UI

## Existing architecture

Next.js App Router client pages use the existing sessionStorage authentication helpers, JWT expiration checks, role checks, `NEXT_PUBLIC_API_URL`, `ApiResponse` envelope, shared Header, and Tailwind component classes. Backend authorization and the tool policy remain the security boundary. No backend contracts, tools, model configuration, database schema, or auth architecture changed.

```text
Next.js /assistant
    |
    v
ASP.NET Core /api/agent/chat
    |
    v
AgentChatService -> Qwen -> Tool / Policy
                              |
                              +-- READ -> trusted execution -> Database
                              |
                              +-- WRITE -> Pending (not published)
                                             |
                                             v
                               Next.js /admin/agent-actions
                                             |
                                        Approve / Reject
                                             |
                                             v
                                     Trusted execution
                                             |
                                             v
                                          Database
```

## Frontend flow

- `/assistant` permits User and Admin. Submit `{message}` with the existing Bearer JWT to `/api/agent/chat`. The transcript is component state only, and each request is an independent backend conversation.
- While waiting, show `Thinking...`, not fabricated tool progress. A failed request retains the input for retry. Each successful answer can expand safe tool name/success/code summaries; no raw prompts or payloads are rendered there.
- A returned pending proposal is explicitly labeled not published. Only Admin sees the link to review proposals.
- `/admin/agent-actions` permits Admin only. GET pending and display the normalized recipe title, description, category, servings, times, ingredients, ordered steps and tools, with short action ID, status and creation time. Identity IDs, policy metadata and payload hashes are not displayed.
- Approve/reject POST only the stored action ID, without payload overrides. Disable both controls for that action immediately using a synchronous lock plus state. Success removes the item and refreshes the queue. Executed results link to the created recipe. Rejected results explicitly say no recipe was published. Conflicts/failures refresh the queue without claiming success.
- Missing/expired sessions save the intended route and redirect to login. API 401 clears the session; API 403 shows a permission error. Safe API errors, timeout, network failure, malformed JSON/envelopes and invalid response shapes are handled visibly.

## Run and manual verification

1. Start the backend with the existing Development configuration and database. Start Ollama with the configured Qwen model available. Agent must be enabled. Use existing User/Admin accounts.
2. From `frontend`, run `npm run dev`, then open `http://localhost:3000/assistant`.
3. Logged out: confirm login redirect and intended path. Login as User; send `Find me a chicken recipe.` Expand Agent activity and confirm the database-backed search answer.
4. As User, ask to create a recipe. Confirm no approval controls, pending action or published recipe. Directly visiting `/admin/agent-actions` redirects home; backend still rejects User approval requests.
5. Login as Admin. Ask to propose a new uniquely named recipe using existing category/ingredient/unit names. Confirm Pending/not published. Verify the recipe does not yet appear through the regular Recipes API/UI.
6. Open `/admin/agent-actions`, review all details, then Approve. Confirm Executed, created Recipe ID and working View recipe link. Repeated clicks cannot execute the same action twice.
7. Create another uniquely named proposal and Reject it. Confirm removal from the queue and no new recipe.
8. Stop Ollama or the API temporarily to inspect a visible error and retained input. Restore the service. Test a expired session and a conflict from two tabs. No JWTs or internal errors should appear in the page.
9. Check both pages at desktop and mobile widths; Header remains visible, long content wraps, and controls remain accessible.

## Checks

Run `npm run lint`, `npm run build`, and `node --test tests/agent-response.test.cjs` from frontend. The focused parser tests use Node's built-in runner and existing TypeScript, with no added dependency or test framework. Run the complete backend integration suite as usual.

Browser smoke testing can use a disposable EF InMemory integration-test host with a deterministic model double to exercise actual backend auth, validation, policy, proposal and approval endpoints without altering development/production data. This verifies UI integration, not live Qwen language behavior. Live Qwen remains a separate manual check; prior live model checks are documented in LOCAL_AGENT.md and AGENT_APPROVALS.md.

### Verified on 2026-10-05

- Backend regression suite: 152 passed, zero failures; backend compilation succeeded.
- Frontend lint and production build passed. Build emitted only the existing outdated Browserslist dataset advisory.
- Five Node response-contract tests passed.
- Headless Edge against the real frontend and a disposable InMemory backend fixture passed: User read, denied User write, User blocked from approval page, Admin Pending without publication, informed review, approval, working recipe-details link, repeated approval 409, rejection with unchanged recipe count, malformed JSON, network failure, model-timeout response, retained retry input, 401 session clearing, and mobile layout with no page errors.
- Browser fixture used a deterministic model double; live Qwen was not rerun for Mission 5. Screenshots were visually inspected for desktop review and mobile chat. Temporary fixture files are outside the repository and were not committed. Development/production database records were not touched.

## Limitations

Conversation history disappears on page unmount/reload; messages are not passed back as model history. Pending actions remain in-memory and disappear on backend restart. The frontend has no streaming/progress feed and shows only genuine request-level waiting. No new write tools, durable actions, cross-replica synchronization or Mission 6 functionality is included.
