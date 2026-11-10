/* eslint-disable @typescript-eslint/no-require-imports */
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const Module = require("node:module");
const ts = require("typescript");
const filename = path.resolve(__dirname, "../src/lib/agent-request.ts");
const loaded = new Module(filename, module);
loaded._compile(ts.transpileModule(fs.readFileSync(filename, "utf8"), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020 }
}).outputText, filename);
const { createTurnRequestId } = loaded.exports;

test("uses native UUID generation where available", () => {
  assert.equal(createTurnRequestId({ randomUUID: () => "native-id", getRandomValues: () => { throw new Error("Unexpected fallback"); } }), "native-id");
  const ids = Array.from({ length: 100 }, () => createTurnRequestId());
  assert.equal(new Set(ids).size, 100);
});

test("creates valid v4 request IDs without randomUUID on HTTP LAN origins", () => {
  const id = createTurnRequestId({ getRandomValues: bytes => { bytes.forEach((_, i) => bytes[i] = i); return bytes; } });
  assert.equal(id, "00010203-0405-4607-8809-0a0b0c0d0e0f");
  assert.match(id, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
});
