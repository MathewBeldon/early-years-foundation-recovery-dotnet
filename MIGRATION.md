# Rails to ASP.NET Core migration platform

This repository runs the current Rails service as the behavioural reference, an intentionally empty ASP.NET Core candidate, and a YARP gateway that sends every application route to Rails. The parity harness is independent TypeScript/Playwright code. No business route has moved to .NET.

## Windows prerequisites

Use Windows 11 (or a supported Windows 10 build), WSL2, Podman Desktop and Git Bash. In elevated PowerShell:

```powershell
wsl --update
wsl --install --no-distribution
```

Restart Windows, install [Podman Desktop for Windows](https://podman-desktop.io/docs/installation/windows-install), then create a WSL2-backed machine. Allocate 6–8 CPUs, 12–16 GB effective WSL memory and 80–120 GB disk. Use rootless mode. Podman’s WSL machine metadata may display a non-settable 2 GiB value even when WSL has more memory; verify effective memory with:

```powershell
wsl -d podman-machine-default -- free -h
podman info --format "rootless={{.Host.Security.Rootless}}"
```

If WSL has too little effective memory, configure the global WSL2 `memory` setting in `%UserProfile%\.wslconfig`, run `wsl --shutdown`, and restart the Podman machine. Do not pass `--cpus` or `--memory` to `podman machine set` on Podman 5.8 WSL machines: those options are reported as unsupported. Rootless mode can be changed separately:

```powershell
podman machine stop
podman machine set --rootful=false podman-machine-default
podman machine start
```

Install Compose in Podman Desktop under **Settings → Resources → Compose → Setup**. `podman compose` delegates to that provider. Verify:

```powershell
podman version
podman machine list
podman system connection list
podman compose version
```

On a corporate VPN, recreate/configure the machine with user-mode networking if DNS, outbound access, or published ports are unreliable:

```powershell
podman machine stop
podman machine set --user-mode-networking=true podman-machine-default
podman machine start
```

## Services and URLs

| Service | URL | Purpose |
|---|---|---|
| Rails reference | <http://localhost:3000> | Current behavioural source of truth |
| .NET candidate | <http://localhost:5000/health> | Candidate liveness; application paths intentionally return 404 |
| YARP gateway | <http://localhost:8080> | Public migration entry point; Rails catch-all |
| Gateway health | <http://localhost:8080/gateway-health> | Gateway-owned, never proxied |
| PostgreSQL | `localhost:${DB_PORT:-5432}` | Existing Rails-owned schema |
| One Login simulator | <http://localhost:4000> | Local identity provider |
| Playwright report | <http://localhost:9323> | Available while `parity-report` is running |

## Day-to-day commands

Run the Bash commands from Git Bash. PowerShell equivalents use `scripts/migration.ps1`.

| Task | Git Bash | PowerShell |
|---|---|---|
| Start Rails, collector, simulator, .NET and gateway | `bash bin/migration-up` | `.\scripts\migration.ps1 up` |
| Stop the complete project | `bash bin/migration-down` | `.\scripts\migration.ps1 down` |
| Rebuild application images | `bash bin/migration-build` | `.\scripts\migration.ps1 build` |
| Run Rails and .NET tests | `bash bin/migration-test` | `.\scripts\migration.ps1 test` |
| Run the complete migration validation and retain evidence | `bash bin/migration-validate` | `.\scripts\migration.ps1 validate` |
| Type-check and run platform parity controls (journeys excluded) | `bash bin/parity-test` | `.\scripts\migration.ps1 parity-test` |
| Run standalone registration journeys (direct Rails) | `bash bin/parity-journey` | `.\scripts\migration.ps1 parity-journey` |
| Run registration journeys through YARP | `bash bin/parity-journey-gateway` | `.\scripts\migration.ps1 parity-journey-gateway` |
| Update visual baselines | `bash bin/parity-update-baselines` | `.\scripts\migration.ps1 parity-update-baselines` |
| Restore the canonical parity DB | `bash bin/parity-reset` | `.\scripts\migration.ps1 parity-reset` |
| Serve the last Playwright report | `bash bin/parity-report` | `.\scripts\migration.ps1 parity-report` |
| Regenerate/check the route manifest | `bash bin/migration-manifest` | `.\scripts\migration.ps1 manifest` |
| Verify the Contentful schema/seed | `bash bin/migration contentful-check` | `.\scripts\migration.ps1 contentful-check` |
| Seed a dedicated test Contentful environment | `bash bin/migration contentful-seed` | `.\scripts\migration.ps1 contentful-seed` |

The explicit Compose equivalent for startup is:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --build --detach app gov-one-simulator otel-collector dotnet gateway
```

The repository scripts also handle a Podman Desktop 1.22 / Podman 5.8 WSL failure where the Docker-compatible named pipe is absent even though the native Podman client works. On Windows only, and only when `DOCKER_HOST` is unset, they start a rootless API service bound to `127.0.0.1:2375` inside the Podman machine and scope `DOCKER_HOST` to the script process. For a manual Compose command, initialise the same bridge in the current PowerShell session:

```powershell
podman machine ssh podman-machine-default "systemctl --user is-active --quiet podman-migration-api.service || systemd-run --user --unit=podman-migration-api --collect podman system service --time=0 tcp:127.0.0.1:2375"
$env:DOCKER_HOST = "tcp://127.0.0.1:2375"
```

It listens on the VM loopback interface, not the LAN. Stop it when no longer needed with:

```powershell
podman machine ssh podman-machine-default systemctl --user stop podman-migration-api.service
Remove-Item Env:DOCKER_HOST -ErrorAction SilentlyContinue
```

Rebuild one service and recreate it:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery build gateway
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --detach --no-deps gateway
```

Enable the Rails worker only for slices that exercise Rails-owned jobs:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --detach worker
```

## Tests and evidence

Rails tests always execute inside the Rails test image:

```bash
bin/podman-rspec
bin/podman-rspec spec/system/cookies_spec.rb
```

.NET tests execute in the pinned SDK container when using `migration-test`. A focused host diagnostic is also possible when the approved SDK is installed:

```powershell
dotnet test dotnet/EarlyYears.sln --configuration Release
```

Platform parity controls are behind the `parity` profile and are not started by ordinary development. This command type-checks the harness and runs smoke, gateway, real-route contract, comparator qualification, visual, accessibility-control, database-guard and side-effect-control tests. Product journeys are deliberately separate.

```powershell
New-Item -ItemType Directory -Force tmp/parity/test-results,tmp/parity/reports,tmp/parity/side-effects | Out-Null
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml -f compose.parity.yml --project-name recovery --profile parity run --rm parity
```

### Standalone registration journeys

Product journeys live under `parity/tests/journeys/`, with concurrent-user authentication coverage under `parity/tests/concurrency/`. They are **target-agnostic**: they only need `TARGET_BASE_URL` and the GOV.UK One Login simulator. They are not discovered by `parity-test`; run them explicitly with the journey commands below. They do not compare Rails vs .NET and do not require the gateway, .NET candidate, or side-effect sink.

Compose-based parity runs start the Rails reference in `RAILS_ENV=parity`: production-style eager/class loading with HTTP simulator access, synthetic credentials, external delivery disabled, and application/Contentful caches kept deterministic. Ordinary `migration-up` development remains unchanged and is the explicit development-mode entry point.

CI executes `migration/tools/check-rails-parity-environment.rb` against the running Rails reference and rejects production/live mode, HTTPS enforcement, stateful caches, external mail delivery, or an unexpected database host/name.

Registration coverage mirrors the Rails branch matrix (happy paths) plus `registration-review.spec.ts` for Back links and Check your answers Change flows (England↔elsewhere unlocking LA, no-role↔early-years unlocking role/experience, name/email edits, review-mode Back discard).

My modules coverage (`tests/journeys/my-modules.spec.ts`) covers post-registration entry: auth gates, empty Available/Upcoming lists, Start module through interruption to `1-1`, in-progress/Resume, and draft-module redirect.

Module completion (`tests/journeys/module-completion.spec.ts`) walks Contentful seed module `alpha` end-to-end (content, formative, summative pass/fail, confidence, skip feedback, certificate / retake), mirroring Rails AST fixtures under `spec/support/ast/`.

Product journeys under `parity/tests/journeys/` cover learner-facing Rails system behaviour: registration through module complete/fail/feedback/confidence/formative/overview/certificate, account/close/preferences, learning log, cookies, static/about/errors, course feedback, and sign-in. `migration/parity-coverage.json` records a machine-checked covered/partial/excluded decision for every Rails system spec; `node migration/tools/check-parity-coverage.mjs` fails when a spec or referenced journey is missing. `migration/request-coverage.json` independently inventories every recursive Rails request spec, its behaviours, evidence and pre-migration requirement; the checker makes bot, webhook and OIDC callback contracts impossible to omit silently. Partial, source-only and excluded behaviours state the gate that must close before the affected route boundary can move.

### Complete validation and first-green archive

Run `bash bin/migration-validate` only when the non-production Contentful configuration required by the Rails suite is available. It checks all manifests, verifies Contentful fixtures, runs Rails and .NET tests at the normal 92% coverage gate, reruns only session characterization with `TIMEOUT_IN_MINUTES=1` and a scoped focused-run coverage threshold, executes platform controls, resets the Rails-owned journey database between direct and gateway suites, and then archives checksummed evidence under `tmp/parity/validation-archives`. The baseline journey suites use one Playwright worker to isolate scenarios from suite-level load; the dedicated concurrency spec still opens two simultaneous One Login sessions.

The first successful run creates `tmp/parity/validation-archives/first-green.json`; later successful runs do not overwrite that baseline pointer. Failed runs are archived too. Generated evidence remains ignored by Git and CI uploads its equivalent as a retained artifact.

Every journey page enforces a main-frame origin allow-list containing only `TARGET_BASE_URL` and the GOV.UK One Login simulator. In particular, a gateway journey fails if any redirect escapes directly to Rails even when its final path would otherwise match. CI retries once for diagnostics but treats every flaky retry as a failure.

CI recreates the Rails-owned development database before the direct and gateway suites, records matching schema fingerprints, and fingerprints both the source-controlled Contentful fixtures and the exact remote test delivery content. Each Playwright report includes `run-metadata.json` with target origin, commit/run identity, fixture fingerprints, and runtime versions. Raw reports and provenance are retained as uniquely named CI artifacts for 90 days.

With the migration stack already up (`app` + `gov-one-simulator`):

```powershell
.\scripts\migration.ps1 parity-journey
# Same suite via YARP (Rails catch-all until routes move to .NET):
.\scripts\migration.ps1 parity-journey-gateway

# Target one file / title / override workers (default journey workers=6; simulator identity is bound to each authorize flow):
.\scripts\migration.ps1 parity-journey tests/journeys/registration-review.spec.ts
.\scripts\migration.ps1 parity-journey -g "clears local authority"
$env:PLAYWRIGHT_WORKERS = '2'; .\scripts\migration.ps1 parity-journey tests/journeys/my-modules.spec.ts
# or: .\scripts\migration.ps1 parity-journey tests/journeys/my-modules.spec.ts --workers=2
```

An option-only invocation such as `.\scripts\migration.ps1 parity-journey --workers=6` applies that option to the default `tests/journeys` and `tests/concurrency` targets. A leading explicit test path replaces those defaults.

To watch the browser on your desktop (host Chromium; not the container):

```powershell
.\scripts\migration.ps1 parity-journey-headed
# or interactive Playwright UI:
# cd parity
# $env:TARGET_BASE_URL='http://localhost:3000'
# $env:GOV_ONE_SIMULATOR_URL='http://localhost:4000'
# $env:PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR='1'
# npm run test:journeys:ui
```

Rails authorize links still point at `http://gov-one-simulator:4000`. The headed runner maps that hostname to `127.0.0.1` inside Chromium (the simulator itself was fine on `:4000`; DNS from the host browser was the failure mode).

Override the application under test when a route moves to .NET or the gateway:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml -f compose.parity.yml --project-name recovery --profile journeys run --rm --env TARGET_BASE_URL=http://gateway.recovery.internal:8080 parity-journeys
```

Host-run (against published `localhost:3000`) is also supported once Node deps are installed in `parity/`:

```powershell
cd parity
$env:TARGET_BASE_URL = 'http://localhost:3000'
$env:GOV_ONE_SIMULATOR_URL = 'http://localhost:4000'
npm run test:journeys
```

Host headed runs set `PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR=1` so Chromium resolves `gov-one-simulator` to `127.0.0.1`. The containerized `parity-journey` path uses Compose DNS and does not need that mapping.

The England / local-authority happy path needs Contentful registration settings (`CONTENTFUL_TEST_*`). The custom-setting (outside England) path and the unauthenticated redirect check do not.

Failure traces, screenshots, video and HTML/JUnit reports are written beneath `tmp/parity`. View a trace directly:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml -f compose.parity.yml --project-name recovery --profile parity run --rm --service-ports parity npx playwright show-trace test-results/<trace.zip> --host 0.0.0.0
```

Visual baselines use the exact Playwright 1.61.1 Chromium build. Package and container versions must change together. Differences are exact (`maxDiffPixels: 0`); add a selector to `parity/fixtures/dynamic-regions.json` only with a specific justification.

## Contentful test schema and content

`contentful/contentful-export/content-types.json` is the authoritative, schema-only export. The generated import preserves its 10 content types, editor interfaces, locale and tag, then adds one synthetic asset and the deterministic `alpha`, `bravo`, `charlie`, `delta`, course-feedback, static-page, resource and registration-setting entries required by Rails tests. It does not use the outdated `cms/migrate` files or copy production entries.

Verify the generated import without credentials:

```powershell
.\scripts\migration.ps1 contentful-check
```

To seed, create a separate Contentful space/environment and a temporary personal Management API token. A token cannot be retrieved after Contentful first displays it; create a new one if necessary. Set it only for this PowerShell session:

```powershell
$env:CONTENTFUL_TEST_SPACE = '<destination-space-id>'
$env:CONTENTFUL_TEST_ENVIRONMENT = 'master'
$env:CONTENTFUL_TEST_MANAGEMENT_TOKEN = '<temporary-management-token>'
.\scripts\migration.ps1 contentful-seed
Remove-Item Env:CONTENTFUL_TEST_MANAGEMENT_TOKEN
```

The importer permits `master` in a dedicated destination space, but refuses the exported HFEYP source space and environments named `production` or `staging`. It also requires the script-provided `CONTENTFUL_ALLOW_TEST_SEED=true` confirmation. After seeding, create read-only Delivery and Preview tokens for the destination, store them only in ignored `.env`/CI secrets, and run `.\scripts\migration.ps1 test`. See `contentful/README.md` for the complete safety model.

## Deterministic database comparisons

Rails remains the only migration/schema owner. The .NET project has no EF Core dependency or migrations and rejects any `Database:SchemaOwner` value other than `Rails`.

Prepare the synthetic Rails template database before the first reset:

```powershell
podman compose -f docker-compose.yml -f docker-compose.test.yml --project-name recovery run --rm app bin/rails db:prepare
```

`parity-reset` then performs guarded operations only against an explicitly allowed local/Compose host and databases ending `_test` or `_parity`. The target and template must be different. It takes an advisory lock, disconnects the target, clones `PARITY_TEMPLATE_DATABASE`, and applies `parity/fixtures/canonical.sql`. Write scenarios additionally require an application table in the capture plan and a callback that verifies each candidate is connected to the expected parity database. The coordinator in `parity/tests/database/write-scenario.ts` always runs:

1. restore fixture;
2. Rails scenario;
3. canonical database capture;
4. restore identical fixture;
5. .NET scenario;
6. canonical database capture;
7. strict comparison.

The current development Rails and .NET services use the development database. Do not add a write comparison until dedicated candidate services are configured against `PARITY_DATABASE_URL` and their target-verification callback can prove that connection. The empty canonical fixture and schema-only capture plan are platform placeholders, not migration evidence.

Do not point `PARITY_DATABASE_URL` at a development, staging or production database. Do not run Rails and .NET writes concurrently.

## Configuration

Local defaults are synthetic. Override them in an uncommitted `.env`; never commit keys or production data.

| Variable | Purpose / safe default |
|---|---|
| `APPLICATION_INSIGHTS_CONNECTION_STRING` | Optional; local migration Compose uses the debug OTel exporter instead |
| `DB_PORT` | Host PostgreSQL port, default `5432` |
| `RAILS_BASE_URL` | Parity Rails target, default in Compose `http://app:3000` |
| `DOTNET_BASE_URL` | Parity .NET target, default `http://dotnet:8080` |
| `GATEWAY_BASE_URL` | Parity gateway target, default `http://gateway:8080` |
| `SIDE_EFFECT_SINK_URL` | Local fake receiver, default `http://side-effect-sink:9090` |
| `PARITY_DATABASE_URL` | Dedicated `_parity` database only |
| `PARITY_TEMPLATE_DATABASE` | Rails-owned synthetic template database |
| `PARITY_ALLOW_DATABASE_RESET` | Must be exactly `true`; scripts set it only for the reset command |
| `AUTHENTICATED_STORAGE_STATE` | Future simulator-created browser state; placeholder is disabled when absent |
| `CONTENTFUL_TEST_SPACE` | Non-production Contentful space ID; preferred test alias, falling back to existing `CONTENTFUL_SPACE` |
| `CONTENTFUL_TEST_DELIVERY_TOKEN` | Preferred test alias for the read-only delivery token; falls back to `CONTENTFUL_DELIVERY_TOKEN` |
| `CONTENTFUL_TEST_PREVIEW_TOKEN` | Preferred test alias for the preview token; falls back to `CONTENTFUL_PREVIEW_TOKEN` |
| `CONTENTFUL_TEST_ENVIRONMENT` | Preferred test environment alias; falls back to `CONTENTFUL_ENVIRONMENT`, then `test` |
| `CONTENTFUL_TEST_MANAGEMENT_TOKEN` | Temporary destination-only token consumed by `contentful-seed`; remove it from the shell immediately after import |
| `CONTENTFUL_ALLOW_TEST_SEED` | Destructive-operation guard; repository scripts set it only for the explicit seed command |

## Routing a reviewed slice

Do not route an authenticated fragment until session ownership and the complete journey boundary are approved. After every gate in `migration/acceptance-gates.md` passes, add a source-controlled route above the catch-all in `gateway/appsettings.json`:

```json
"dotnet-approved-boundary": {
  "ClusterId": "dotnet",
  "Order": 0,
  "Match": { "Path": "/approved-boundary/{**catch-all}" },
  "Transforms": [
    { "RequestHeaderOriginalHost": "true" },
    { "X-Forwarded": "Append", "HeaderPrefix": "X-Forwarded-" }
  ]
}
```

Never add a public header-based backend override. To roll back, remove that specific route and rebuild/restart `gateway`; the order-1000 Rails catch-all immediately owns the path again. Keep the .NET route state and evidence in `migration/route-manifest.json` and its slice record.

## Logs and troubleshooting

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery ps
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery logs --follow app dotnet gateway otel-collector
```

- **Bind mounts do not refresh:** polling is already enabled for `dotnet watch`. Keep the repository on a local NTFS drive, restart the service, and avoid network shares.
- **Compose reports a Podman named-pipe error:** confirm the rootless connection is default with `podman system connection list`. Use the repository scripts, which initialise the localhost-only compatibility bridge documented above. Podman itself can work while a Docker-compatible provider pipe is unavailable.
- **VPN/DNS failures:** enable user-mode networking, restart WSL/Podman, and ensure corporate CA certificates are available to the machine. Do not disable TLS verification in committed files.
- **Port conflict:** set `DB_PORT` for PostgreSQL or stop the process using ports 3000, 4000, 5000, 8080 or 9323. Keep documented service ports unchanged unless the whole team agrees.
- **Unhealthy Rails:** inspect `app`, `db`, `gov-one-simulator` and collector logs; Rails has a 60-second health start period for asset/dependency startup.
- **Stale package cache:** remove only the named migration cache volume after stopping the project. `migration-down` intentionally preserves volumes.
- **Manifest stale:** rebuild `recovery:dev`, then run `migration-manifest`; the generator boots the exact Rails route set and source-digests controllers, models and specs.
- **`key must be 16 bytes` before Rails tests:** use the migration test Compose overlay, which explicitly masks `RAILS_MASTER_KEY` and supplies non-production integration settings. Tests must never decrypt `config/credentials.yml.enc` or inherit a production Rails key from `.env`. Never substitute a Contentful token for a Rails key.

Architecture risks, gates, route evidence and current status live under `migration/`.
