/* eslint-disable @typescript-eslint/no-require-imports */
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const Module = require("node:module");
const ts = require("typescript");
const filename = path.resolve(__dirname, "../src/lib/auth.ts");
const loaded = new Module(filename, module);
loaded._compile(ts.transpileModule(fs.readFileSync(filename, "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText, filename);
const auth = loaded.exports;
const storage = () => {
  const values = new Map();
  return { getItem: key => values.get(key) ?? null, setItem: (key, value) => values.set(key, value), removeItem: key => values.delete(key) };
};
const token = exp => `header.${Buffer.from(JSON.stringify({ exp })).toString("base64url")}.signature`;

test("session switches and logout notify agent guards and clear private account data", () => {
  global.window = new EventTarget();
  global.sessionStorage = storage();
  global.localStorage = storage();
  let changes = 0;
  window.addEventListener(auth.AUTH_SESSION_CHANGED, () => changes++);
  const valid = token(Math.floor(Date.now() / 1000) + 300);
  auth.saveAuthSession({ token: valid, name: "First", email: "first@example.com", role: "User" });
  assert.equal(auth.getValidToken(), valid);
  assert.equal(changes, 1);
  auth.saveAuthSession({ token: valid + "new-session", name: "Second", email: "second@example.com", role: "Admin" });
  assert.equal(auth.getName(), "Second");
  assert.equal(changes, 2);
  auth.clearAuthSession();
  assert.equal(changes, 3);
  for (const key of ["token", "name", "email", "role"]) assert.equal(sessionStorage.getItem(key), null);
  auth.getValidToken();
  auth.clearAuthSession();
  assert.equal(changes, 3); // No recursive logout events when guards check an empty session.
  delete global.window;
});

test("expired sessions trigger one privacy reset rather than retaining chat access", () => {
  global.window = new EventTarget();
  global.sessionStorage = storage();
  global.localStorage = storage();
  let changes = 0;
  auth.saveAuthSession({ token: token(1), name: "Expired", email: "expired@example.com", role: "User" });
  window.addEventListener(auth.AUTH_SESSION_CHANGED, () => changes++);
  assert.equal(auth.getValidToken(), null);
  assert.equal(auth.getName(), null);
  assert.equal(auth.getRole(), null);
  assert.equal(changes, 1);
  delete global.window;
});
