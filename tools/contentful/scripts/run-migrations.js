const { loadEnv, trimValue } = require("./load-env");

loadEnv();

const fs = require("fs");
const path = require("path");
const { runMigration } = require("contentful-migration");

const migrateDir = path.join(__dirname, "..", "migrate");

async function main() {
  if (!fs.existsSync(migrateDir)) {
    console.log("No migrate/ directory yet. Add original migration files in Phase 2.");
    return;
  }

  const files = fs
    .readdirSync(migrateDir)
    .filter((file) => file.endsWith(".js"))
    .sort();

  if (files.length === 0) {
    console.log("No migration files found in migrate/. Add original migrations in Phase 2.");
    return;
  }

  const spaceId = trimValue(process.env.CONTENTFUL_SPACE_ID ?? "");
  const environmentId = trimValue(process.env.CONTENTFUL_ENVIRONMENT ?? "") || "master";
  const token = trimValue(process.env.CONTENTFUL_MANAGEMENT_TOKEN ?? "");

  if (!spaceId || !token) {
    console.error("Set CONTENTFUL_SPACE_ID and CONTENTFUL_MANAGEMENT_TOKEN in .env before running migrations.");
    console.error("Run npm run verify first to check your credentials.");
    process.exit(1);
  }

  if (!token.startsWith("CFPAT-")) {
    console.error("CONTENTFUL_MANAGEMENT_TOKEN should be a CMA personal access token (starts with CFPAT-).");
    console.error("Create one at Contentful → open your space → Settings → CMA tokens.");
    process.exit(1);
  }

  for (const file of files) {
    const migrationPath = path.join(migrateDir, file);
    console.log(`Running ${file}...`);
    await runMigration({
      filePath: migrationPath,
      spaceId,
      environmentId,
      accessToken: token,
      yes: true,
    });
  }

  console.log("Migrations complete.");
}

main().catch((error) => {
  console.error(error.message ?? error);
  process.exit(1);
});
