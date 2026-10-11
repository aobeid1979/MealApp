/* eslint-disable @typescript-eslint/no-require-imports */
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const Module = require("node:module");
const ts = require("typescript");

// Use Node's built-in runner and the existing TypeScript dependency, not a new test framework.
const filename = path.resolve(__dirname, "../src/lib/agent-response.ts");
const loaded = new Module(filename, module);
loaded._compile(ts.transpileModule(fs.readFileSync(filename, "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText, filename);
const { readAgentResponse, isChatReply, isActionList, isAction } = loaded.exports;
const envelope = (result, status = 200, errors = []) => new Response(JSON.stringify({
  isSuccess: status === 200, result, errorMessages: errors, statusCode: status
}), { status });
const reply = { answer: "Chicken Dinner", toolCalls: [{ tool: "search_recipes", isSuccess: true, code: "success" }] };
const action = { actionId: "test-action", actionType: "create_recipe_draft", createdAt: new Date().toISOString(), status: "Pending", recipeId: null,
  payload: { name: "Dinner", category: "Dinner", servings: 4, preparationTimeMinutes: 10, cookingTimeMinutes: 20,
    ingredients: [{ ingredient: "Chicken", quantity: 500, unit: "Gram" }], steps: [{ stepNumber: 1, instruction: "Cook." }], tools: ["Pan"] } };

test("unwraps safe chat response and pending proposal", async () => {
  assert.deepEqual(await readAgentResponse(envelope(reply), isChatReply), reply);
  assert.equal(isChatReply({ ...reply, pendingAction: { actionId: "test", status: "Pending" } }), true);
  assert.equal(isChatReply({ ...reply, pendingAction: { status: "Published" } }), false);
});
test("rejects malformed JSON, missing wrapper, and invalid chat shape", async () => {
  for (const response of [new Response("not json"), new Response(JSON.stringify(reply)), envelope({ answer: "Missing summaries" })]) {
    await assert.rejects(readAgentResponse(response, isChatReply), /invalid response/);
  }
});
test("handles 401 and 403 even without JSON", async () => {
  await assert.rejects(readAgentResponse(new Response("", { status: 401 }), isChatReply), /session has expired/);
  await assert.rejects(readAgentResponse(new Response("", { status: 403 }), isChatReply), /permission/);
});
test("preserves safe API validation, model timeout, conflict, and execution errors", async () => {
  for (const status of [400, 409, 500, 503, 504]) {
    await assert.rejects(readAgentResponse(envelope(null, status, ["Safe error message."]), isChatReply), /Safe error message/);
  }
});
test("validates review payloads before rendering and decision results", async () => {
  assert.deepEqual(await readAgentResponse(envelope([action]), isActionList), [action]);
  assert.equal(isAction({ ...action, status: "Executed", recipeId: 2 }), true);
  assert.equal(isActionList([{ ...action, payload: { ...action.payload, steps: [null] } }]), false);
  assert.equal(isActionList([{ ...action, payload: { ...action.payload, ingredients: null } }]), false);
  assert.equal(isActionList(null), false);
});
