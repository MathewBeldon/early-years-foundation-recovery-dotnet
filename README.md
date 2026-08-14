# Early Years Foundation Recovery (.NET migration workspace)

This repository is a local Rails-to-.NET compatibility workspace. The Rails PostgreSQL schema at migration version `20260214120000` is authoritative. The web application validates that contract at startup and never applies migrations implicitly.

## Prerequisites

- .NET 10 SDK
- Docker Desktop or Podman with Compose support
- PowerShell 7

## Run the .NET application against a Rails-shaped database

Start the ordinary local dependencies, load the Rails schema into PostgreSQL, apply the additive .NET migration, then run the app:

```powershell
docker compose up -d
dotnet run --project src/EarlyYearsFoundationRecovery.Web -- --schema-preflight
dotnet run --project src/EarlyYearsFoundationRecovery.Web -- --migrate
dotnet run --project src/EarlyYearsFoundationRecovery.Web
```

The preflight command exits with code 2 when any required Rails table is absent or `schema_migrations` is older than `20260214120000`. `--migrate` records the existing Rails schema as a no-op EF baseline, then applies only .NET-owned additions such as `background_jobs` and `mail_events.notification_id`. Rails Devise and Que columns/tables are deliberately preserved and unmapped.

The app is at <http://localhost:5000>, health is at <http://localhost:5000/health>, and the One Login simulator is at <http://localhost:3333>.

## One-command parity environment

The parity environment creates a detached worktree at the last Rails commit, two PostgreSQL databases, Rails, .NET, the One Login simulator, and recording Notify/Contentful fakes. For a clean one-command rehearsal, use `reset`; it creates both databases from the Rails migrations and the same synthetic SQL fixture before starting either application:

```powershell
./parity.ps1 reset
./parity.ps1 test
```

Use `./parity.ps1 reset` before each scenario group to recreate both databases from the same Rails schema and synthetic fixture manifest. Use `./parity.ps1 down` to stop the environment. `reset` deletes only the parity Compose volumes.

The committed fixture contract is [synthetic-fixtures.json](parity/fixtures/synthetic-fixtures.json). It contains no production-derived data. The recording fake exposes requests at `http://localhost:4010/_requests` for Notify and `http://localhost:4020/_requests` for Contentful.

The detached Rails worktree is pinned to commit `33c73750`; changing that pin is an explicit schema-contract update and must be reviewed together with intentional fixture-manifest changes. Never use production exports as parity fixtures.

## Playwright and certificates

Restore/build once, then install the pinned Chromium version:

```powershell
dotnet build EarlyYearsFoundationRecovery.slnx
pwsh src/EarlyYearsFoundationRecovery.ParityTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

The same browser installation is used for parity tests and certificate PDFs. Certificates are generated as real PDFs with extractable recipient and module text. If generation reports that Chromium is missing, rerun the install command for the active build configuration.

## Test suites and parity report

```powershell
dotnet restore EarlyYearsFoundationRecovery.slnx --force-evaluate
dotnet tool restore
dotnet build EarlyYearsFoundationRecovery.slnx --no-restore -warnaserror
dotnet test EarlyYearsFoundationRecovery.slnx --no-build

$env:RAILS_BASE_URL = "http://localhost:3000"
$env:DOTNET_BASE_URL = "http://localhost:5000"
dotnet test src/EarlyYearsFoundationRecovery.ParityTests
```

The parity suite compares semantic status, redirects, headings, validation messages, navigation, MIME types, and download filenames. It normalizes antiforgery values, framework cookie names, generated IDs, timestamps, and insignificant whitespace. Results are written to `TestResults/parity-report.json`, including every accepted normalization and unresolved difference.

The committed cross-application manifest currently covers 19 public, error, health, authentication-redirect, feedback-introduction, and static-content scenarios. The ordinary .NET suites cover registration, modules, assessments, learning logs, account changes, callbacks, and persistence, but those authenticated journeys are not yet driven through both live applications. Do not treat the migration as production-ready until equivalent Rails/.NET scenarios for those journey groups have been added to the manifest and pass.

`./parity/reconcile.ps1` compares canonical row counts, ID bounds, completions, and result values across both databases and writes `TestResults/database-reconciliation.json`. It is run automatically by `./parity.ps1 test`.

## Durable jobs, integrations, and exports

`IBackgroundJobService.EnqueueAsync(jobType, payload)` writes to PostgreSQL. Workers claim due jobs with `FOR UPDATE SKIP LOCKED`, retry with backoff, and retain permanent failures. The supported scheduled export type is `dashboard_export`; generated CSVs are stably ordered under `storage/exports/YYYY-MM-DD/`.

Notify uses the GOV.UK Notify email endpoint shape and persists returned notification IDs. Delivery callbacks update both `users.notify_callback` and the matching `mail_events.callback`, matching Rails behavior. Contentful `/release` and `/change` webhooks invalidate caches and persist the original release payload.

## Troubleshooting

- **Database is not Rails-shaped**: load `db/schema.rb` from Rails first; do not run the old create-from-empty EF chain.
- **Rails schema is too old**: migrate Rails to at least `20260214120000`, then rerun preflight.
- **Port already in use**: local defaults are 3000, 3333, 4010, 4020, 5000, 55431 and 55432.
- **Parity Rails build fails**: remove only `parity/.rails-source` with `git worktree remove parity/.rails-source`, then rerun `./parity.ps1 up`.
- **Notify fails locally**: verify `Notify:BaseUrl` points to the fake and inspect `/_requests`.
- **PDF browser missing**: install Chromium using the generated Playwright script shown above.

CI/CD, Azure, Terraform, production deployment, Rails debug/snippet routes, legacy Devise password flows, cross-deployment session continuity, and pixel-perfect rendering are out of scope.
