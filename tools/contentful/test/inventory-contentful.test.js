const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const test = require("node:test");

const script = path.join(__dirname, "..", "scripts", "inventory-contentful.js");
const fixtures = path.join(__dirname, "fixtures");

function runCli(fixture, extraArgs = [], environment = {}) {
  const resultDirectory = fs.mkdtempSync(path.join(os.tmpdir(), "contentful-inventory-"));
  const report = path.join(resultDirectory, "report.json");
  const env = { ...process.env, CONTENTFUL_SPACE_ID: "", CONTENTFUL_DELIVERY_API_KEY: "", ...environment };
  const result = spawnSync(process.execPath, [script, "--fixture", path.join(fixtures, fixture), "--report", report, ...extraArgs], {
    encoding: "utf8",
    env,
  });
  return { ...result, report: fs.existsSync(report) ? JSON.parse(fs.readFileSync(report, "utf8")) : null };
}

test("reports Rails array/object answer shapes and detects multi-select questions", () => {
  const result = runCli("inventory-blocked.json", ["--include-entry-ids"]);
  assert.equal(result.status, 5);
  assert.equal(result.report.questions.options, 5);
  assert.equal(result.report.questions.correctOptions, 3);
  assert.equal(result.report.questions.multiSelect, 1);
  assert.equal(result.report.modules[0].entryId, "module-1");
  assert.deepEqual(result.report.modules[0].pageOrder, ["formative", "summative"]);
  assert.ok(result.report.modules[0].integrity.missingRequiredPageTypes.includes("certificate"));
});

test("classifies malformed answer JSON separately from cutover blockers", () => {
  const result = runCli("inventory-malformed.json");
  assert.equal(result.status, 4);
  assert.equal(result.report.cutover.status, "malformed-content");
  assert.match(result.report.malformedContent[0].reason, /valid JSON/);
  assert.equal(result.report.malformedContent[0].location, "entries[0].answers");

  const withIds = runCli("inventory-malformed.json", ["--include-entry-ids"]);
  assert.match(withIds.report.malformedContent[0].location, /malformed-question/);
});

test("successful fixture produces an aggregate-only report", () => {
  const result = runCli("inventory-empty.json");
  assert.equal(result.status, 0);
  assert.equal(result.report.cutover.status, "ready");
  assert.equal(result.report.readOnly, true);
  assert.equal(Object.hasOwn(result.report, "body"), false);
  assert.equal(Object.hasOwn(result.report, "text"), false);
});

test("does not print configured secrets or authorization material", () => {
  const secret = "CFPAT-test-secret-value";
  const result = runCli("inventory-empty.json", [], {
    CONTENTFUL_SPACE_ID: "safe-space",
    CONTENTFUL_DELIVERY_API_KEY: secret,
  });
  const output = `${result.stdout}\n${result.stderr}\n${JSON.stringify(result.report)}`;
  assert.equal(output.includes(secret), false);
  assert.equal(output.includes("access_token"), false);
  assert.equal(output.includes("Authorization"), false);
});

test("missing configuration has its own exit classification", () => {
  const result = spawnSync(process.execPath, [script, "--report", path.join(os.tmpdir(), "contentful-missing.json")], {
    encoding: "utf8",
    env: { ...process.env, CONTENTFUL_SPACE_ID: "", CONTENTFUL_DELIVERY_API_KEY: "" },
  });
  assert.equal(result.status, 2);
  assert.match(result.stderr, /Missing Contentful configuration/);
});

test("HTTP/authentication failures have a distinct classification without network access", async () => {
  const { fetchEntries, EXIT_CODES } = require("../scripts/inventory-contentful");
  await assert.rejects(
    fetchEntries({ spaceId: "space", environmentId: "master", token: "secret" }, async () => ({
      ok: false,
      status: 401,
      json: async () => ({ message: "unauthorized" }),
    })),
    error => error.exitCode === EXIT_CODES.NETWORK_OR_AUTH,
  );
});

test("sends the Delivery credential in a header rather than the request URL", async () => {
  const { fetchEntries } = require("../scripts/inventory-contentful");
  let capturedUrl;
  let capturedOptions;
  await fetchEntries(
    { spaceId: "space", environmentId: "master", token: "secret-token" },
    async (url, options) => {
      capturedUrl = url.toString();
      capturedOptions = options;
      return {
        ok: true,
        status: 200,
        json: async () => ({ items: [], total: 0 }),
      };
    },
  );

  assert.equal(capturedUrl.includes("secret-token"), false);
  assert.equal(capturedUrl.includes("access_token"), false);
  assert.equal(capturedOptions.headers.Authorization, "Bearer secret-token");
});
