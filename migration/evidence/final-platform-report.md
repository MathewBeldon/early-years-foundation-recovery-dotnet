# Migration platform execution report

Executed locally on 14 July 2026 using Podman 5.8.2, a rootless WSL2 machine and the repository's Compose graph. This report covers the platform only; no business route was migrated.

## Outcomes

- Rails reference, PostgreSQL, GOV.UK One Login simulator, OpenTelemetry collector, ASP.NET Core candidate and YARP gateway built and reached healthy state.
- Rails returned 200 at `/health`; .NET returned 200 at `/health` and 404 for an unmigrated path; gateway-owned health returned 200; gateway `/health` returned the exact Rails health body.
- The pinned Playwright 1.61.1 container completed 10 tests with 1 explicit database-reset test skipped during the ordinary run. Smoke, gateway catch-all, HTTP capture, exact screenshot, accessibility execution/control, database guard and side-effect capture passed.
- The guarded database-reset command passed and cloned the Rails-owned test schema into `early_years_foundation_recovery_parity`; 18 public tables were observed.
- The locked .NET test image passed 2 unit and 4 integration tests.
- The route manifest stale check passed: 114 routes total (83 application, 31 framework), all still owned by Rails.

## Commands executed

Representative final commands (all ran through Podman except static host checks):

```text
podman build --file dotnet/Dockerfile --target test --tag recovery-dotnet:test .
podman compose -f docker-compose.yml -f docker-compose.test.yml --project-name recovery build app
podman compose -f docker-compose.yml -f docker-compose.test.yml --project-name recovery run --name recovery_rspec_debug app rspec
.\scripts\migration.ps1 parity-update-baselines
.\scripts\migration.ps1 parity-reset
.\scripts\migration.ps1 up
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --detach --wait --wait-timeout 240 app gov-one-simulator otel-collector dotnet gateway
.\scripts\migration.ps1 parity-test
node migration/tools/check-manifest.mjs
git diff --check
```

Host diagnostics also executed `wsl ... free -h`, `podman machine list`, `podman machine inspect`, `podman info`, endpoint requests and Compose configuration rendering.

## Route inventory

| Classification | Count |
|---|---:|
| Total | 114 |
| Application | 83 |
| Framework | 31 |
| Anonymous | 53 |
| User | 36 |
| Registered user | 21 |
| Webhook token | 3 |
| Bot token | 1 |
| Medium confidence | 63 |
| Low confidence / unresolved | 51 |

## Unresolved execution and coverage gaps

- The full existing RSpec suite could not execute without the repository's non-production Contentful test-space credentials. With no credentials, RSpec correctly made no production request and stopped during load with two `Contentful::Client` configuration errors (`continue_training_mail_job_spec.rb` and `new_module_mail_job_spec.rb`); 0 examples ran. Compose and CI now accept only `CONTENTFUL_TEST_*` values. Re-run `migration-test` after placing read-only test values in an uncommitted `.env` / CI secrets.
- Consequently, initial browser evidence covers health endpoints and the deterministic harness control document, not Contentful-backed business UI. This is a declared gap, not a migrated feature.
- The GitHub Actions workflow is source-complete but was not executed on GitHub from this local workspace.
- Authenticated Playwright context is intentionally a disabled placeholder until a simulator-owned storage state and session-boundary decision are approved.
- The route inventory is conservative: 51 records remain low confidence and require per-slice executed evidence before migration.
- This report records the original 10-test platform execution. The subsequently expanded product journey suite is separate and is not validated by the outcomes above; current executions belong in timestamped run journals.

## Assumptions

- .NET 10 is the approved current LTS for this repository and is pinned to SDK 10.0.301 / ASP.NET runtime 10.0.9.
- Rails remains sole schema and migration owner; the .NET shell contains no EF Core dependency or migrations.
- Development-only `RAILS_ADDITIONAL_HOSTS` values are limited to internal Compose aliases; production host authorization is unchanged.
- The Podman WSL machine's displayed 2 GiB metadata is not its effective memory limit; WSL reported approximately 15 GiB available. CPU/memory changes through `podman machine set` are unsupported for this WSL provider.

## Reproduction

Follow `MIGRATION.md`. On this Windows/Podman 5.8 setup the scripts automatically initialise the localhost-only rootless Compose API bridge when the Podman Desktop named pipe is unavailable. Then run:

```powershell
.\scripts\migration.ps1 up
.\scripts\migration.ps1 parity-test
.\scripts\migration.ps1 parity-reset
.\scripts\migration.ps1 test # requires CONTENTFUL_TEST_* for the existing Rails suite
```
