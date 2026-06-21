const fs = require("fs");
const path = require("path");

function trimValue(value) {
  return value.trim().replace(/^["']|["']$/g, "");
}

function loadEnv() {
  const envPath = path.join(__dirname, "..", ".env");
  if (!fs.existsSync(envPath)) {
    return;
  }

  const content = fs.readFileSync(envPath, "utf8").replace(/^\uFEFF/, "");

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }

    const separator = trimmed.indexOf("=");
    if (separator === -1) {
      continue;
    }

    const key = trimmed.slice(0, separator).trim();
    const value = trimValue(trimmed.slice(separator + 1));
    if (!process.env[key]) {
      process.env[key] = value;
    }
  }
}

module.exports = { loadEnv, trimValue };
