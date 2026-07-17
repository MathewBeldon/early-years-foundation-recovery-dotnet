# Contentful Rails Test Seed

This directory reproduces the supplied Contentful schema and adds synthetic content required by the Rails suite. The authoritative schema is `contentful-export/content-types.json`; the historical files under `cms/migrate/` are deliberately not used.

The generated import contains all 10 exported content types, editor interfaces, locale and tag, plus one synthetic asset and 146 deterministic entries. It contains no exported entries, production assets, API keys or webhooks.

## Safety boundary

The importer refuses:

- the source HFEYP space recorded in the export;
- environment IDs `production` and `staging`;
- any run without `CONTENTFUL_ALLOW_TEST_SEED=true`.

Use a dedicated destination space. Its `master` environment is supported because the source-space guard remains enforced independently. Importing updates entries with deterministic IDs, so reruns are idempotent, but it is still a write operation.

## Verify locally

From PowerShell:

```powershell
.\scripts\migration.ps1 contentful-check
```

This builds the generated file, verifies its schema SHA-256, and runs 72 structural assertions plus importer safety tests without credentials or network access to Contentful.

## Seed a non-production destination

Create a new personal Contentful Management API token; tokens cannot be retrieved after their initial display. Keep it only in the current shell:

```powershell
$env:CONTENTFUL_TEST_SPACE = '<destination-space-id>'
$env:CONTENTFUL_TEST_ENVIRONMENT = 'master'
$env:CONTENTFUL_TEST_MANAGEMENT_TOKEN = '<temporary-management-token>'
.\scripts\migration.ps1 contentful-seed
Remove-Item Env:CONTENTFUL_TEST_MANAGEMENT_TOKEN
```

After import, create destination Delivery and Preview API tokens and put the non-production values in ignored `.env` variables:

```dotenv
CONTENTFUL_TEST_SPACE=...
CONTENTFUL_TEST_ENVIRONMENT=master
CONTENTFUL_TEST_DELIVERY_TOKEN=...
CONTENTFUL_TEST_PREVIEW_TOKEN=...
```

Do not set `RAILS_MASTER_KEY`; the Rails test overlay explicitly masks it. Run the suite with `.\scripts\migration.ps1 test`.

## Updating the seed

If a new schema-only export is supplied, replace `contentful-export/content-types.json`, then run `npm run build` inside `contentful/` and review both the schema fingerprint and generated import. CI fails when generated files are stale.
