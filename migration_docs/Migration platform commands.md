---
title: Migration platform commands
aliases:
  - Migration commands
  - Platform CLI
  - parity-journey commands
tags:
  - migration
  - commands
  - podman
  - playwright
  - yarp
status: active
last-reviewed: 2026-07-15
---

# Migration platform commands

> [!abstract] Parent note
> Operational cheat-sheet for the coexistence platform described in [[Porting Rails Project To DfE]]. Strategy lives there; day-to-day Compose / Playwright / Contentful commands live here.

Use **Podman Desktop + WSL2**. Prefer the wrappers below over raw Compose unless debugging.

| Shell | Entry point |
|---|---|
| PowerShell | `.\scripts\migration.ps1 <action> [args…]` |
| Git Bash | `bash bin/migration <action> [args…]` or the thin `bin/*` shortcuts |

On Windows, wrappers auto-bridge Compose via `DOCKER_HOST=tcp://127.0.0.1:2375` when needed. See [[Porting Rails Project To DfE]] and repo-root `MIGRATION.md` for machine setup.

---

## Service URLs

| Service | URL | Purpose |
|---|---|---|
| Rails reference | http://localhost:3000 | Behavioural source of truth |
| .NET candidate | http://localhost:5000/health | Liveness; app routes still 404 |
| YARP gateway | http://localhost:8080 | Public entry; Rails catch-all today |
| Gateway health | http://localhost:8080/gateway-health | Gateway-owned, never proxied |
| PostgreSQL | `localhost:${DB_PORT:-5432}` | Rails-owned schema |
| One Login simulator | http://localhost:4000 | Local OIDC IdP |
| Playwright report | http://localhost:9323 | While `parity-report` is running |

---

## Stack lifecycle

| Action | PowerShell | Bash shortcut | What it does |
|---|---|---|---|
| **up** | `.\scripts\migration.ps1 up` | `bash bin/migration-up` | Build/start Rails (`app`), One Login simulator, OTEL collector, .NET, YARP gateway |
| **down** | `.\scripts\migration.ps1 down` | `bash bin/migration-down` | Stop the migration/parity Compose project (`--remove-orphans`) |
| **build** | `.\scripts\migration.ps1 build` | `bash bin/migration-build` | Rebuild `app`, `dotnet`, and `gateway` images |

```powershell
# Explicit Compose equivalent of up
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --build --detach app gov-one-simulator otel-collector dotnet gateway
```

Optional Rails Sidekiq-style worker (only when a slice needs jobs):

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --detach worker
```

---

## Unit / integration tests

| Action | PowerShell | Bash | What it does |
|---|---|---|---|
| **test** | `.\scripts\migration.ps1 test` | `bash bin/migration-test` | Build Rails test image, migrate test DB, run RSpec; build/run .NET test image |

Focused Rails (inside test image):

```bash
bin/podman-rspec
bin/podman-rspec spec/system/cookies_spec.rb
```

Focused .NET (host SDK, if installed):

```powershell
dotnet test dotnet/EarlyYears.sln --configuration Release
```

> [!warning] Contentful for full Rails suite
> Full RSpec needs non-production `CONTENTFUL_TEST_*` (or Contentful) values. Health-only work can run without a complete Contentful space.

---

## Parity harness (differential / infra)

These use Compose profile `parity` (not started by ordinary `up`).

| Action | PowerShell | Bash shortcut | What it does |
|---|---|---|---|
| **parity-test** | `.\scripts\migration.ps1 parity-test` | `bash bin/parity-test` | Full pinned Playwright parity suite (smoke, gateway, contracts, visual, a11y, DB guard, side-effects) |
| **parity-update-baselines** | `.\scripts\migration.ps1 parity-update-baselines` | `bash bin/parity-update-baselines` | Regenerate exact-pixel visual baselines (deliberate; review diffs) |
| **parity-reset** | `.\scripts\migration.ps1 parity-reset` | `bash bin/parity-reset` | Restore canonical parity DB (`PARITY_ALLOW_DATABASE_RESET=true`) |
| **parity-report** | `.\scripts\migration.ps1 parity-report` | `bash bin/parity-report` | Serve last HTML report on :9323 |

Artifacts: `tmp/parity/test-results`, `tmp/parity/reports`, `tmp/parity/side-effects`.

---

## Product journeys (target-agnostic e2e)

Standalone Playwright under `parity/tests/journeys/`. Drive one `TARGET_BASE_URL` + One Login simulator — no Devise helpers, no Rails-vs-.NET compare.

| Action | PowerShell | Bash shortcut | Target | What it does |
|---|---|---|---|---|
| **parity-journey** | `.\scripts\migration.ps1 parity-journey` | `bash bin/parity-journey` | Rails `http://rails.recovery.internal:3000` | All journey specs in-container (headless) |
| **parity-journey-gateway** | `.\scripts\migration.ps1 parity-journey-gateway` | `bash bin/parity-journey-gateway` | YARP `http://gateway.recovery.internal:8080` | Same specs through the gateway |
| **parity-journey-headed** | `.\scripts\migration.ps1 parity-journey-headed` | `bash bin/parity-journey-headed` | Host `http://localhost:3000` | Visible Chromium + slow-mo |
| **parity-journey-gateway-headed** | `.\scripts\migration.ps1 parity-journey-gateway-headed` | `bash bin/parity-journey-gateway-headed` | Host `http://localhost:8080` | Visible Chromium via YARP |

### Current journey files

| Spec | Coverage |
|---|---|
| `tests/journeys/registration.spec.ts` | Registration branch matrix (LA / role / country / custom setting) |
| `tests/journeys/registration-review.spec.ts` | Back links + Check your answers Change flows / unlocked paths |
| `tests/journeys/my-modules.spec.ts` | Post-registration My modules, Start/Resume, draft redirect |
| `tests/journeys/module-completion.spec.ts` | Alpha module e2e: formative → summative pass/fail → certificate / retake |
| `tests/journeys/module-feedback.spec.ts` | Alpha pass path answering module feedback questionnaires |
| `tests/journeys/learning-log.spec.ts` | Learning log empty state, save note, update note |
| `tests/journeys/my-account.spec.ts` | Account details + close-account reason/confirm |
| `tests/journeys/settings.spec.ts` | Cookie banner + cookie policy preferences |
| `tests/journeys/static-pages.spec.ts` | Home, about-training, experts, accessibility, 404 |
| `tests/journeys/account-preferences.spec.ts` | My-account Change: name, location, setting, email, research |
| `tests/journeys/course-feedback.spec.ts` | Course `/feedback` guest + user complete + update |
| `tests/journeys/sign-in.spec.ts` | One Login explainer, already signed-in, sign-out |
| `tests/journeys/certificate.spec.ts` | Incomplete bravo guard + completed alpha certificate |
| `tests/journeys/module-overview.spec.ts` | Overview Start/Resume CTAs + soft-gated topics |
| `tests/journeys/content-pages.spec.ts` | Alpha video page iframe + transcript toggle |
| `tests/journeys/errors-home.spec.ts` | `/500`, `/503`, authenticated home variant |
| `tests/journeys/formative.spec.ts` | Wrong/correct formative lock, resume, empty-submit |
| `tests/journeys/public-content.spec.ts` | `/about/alpha`, guest static smokes, `/whats-new` |
| `tests/journeys/summative.spec.ts` | Intro/passmark, empty submit, pass/fail overview behaviours |
| `tests/journeys/confidence.spec.ts` | Radios, resume, editable after answer |

### Filters and workers

Parity commands run the Rails reference with production-style code loading in the safe `parity` Rails environment. This does not affect the ordinary development stack; `migration-up` remains the development-mode entry point.

An explicit leading test path replaces the default Playwright targets (`tests/journeys` and `tests/concurrency`). Option-only arguments such as `--workers=6` or `-g "title"` modify the default targets instead:

```powershell
# One file
.\scripts\migration.ps1 parity-journey tests/journeys/registration-review.spec.ts

# Title substring
.\scripts\migration.ps1 parity-journey -g "clears local authority"

# Optional workers (default 6 for direct and gateway journeys)
.\scripts\migration.ps1 parity-journey tests/journeys/my-modules.spec.ts --workers=6
$env:PLAYWRIGHT_WORKERS = '6'; .\scripts\migration.ps1 parity-journey
```

> [!note] Journey parallelism
> One Login interactive mode binds identity to each authorize flow. Direct and gateway journeys default to six workers with intra-file parallelism; platform and database checks keep a single-worker fallback. Raise the journey override only after focused concurrency evidence at the proposed level.

Headed UI mode (host):

```powershell
cd parity
$env:TARGET_BASE_URL = 'http://localhost:3000'
$env:GOV_ONE_SIMULATOR_URL = 'http://localhost:4000'
$env:PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR = '1'
npm run test:journeys:ui
```

Headed Chromium maps `gov-one-simulator` → `127.0.0.1` so a Windows hosts entry is not required.

---

## Route manifest

| Action | PowerShell | Bash shortcut | What it does |
|---|---|---|---|
| **manifest** | `.\scripts\migration.ps1 manifest` | `bash bin/migration-manifest` | Regenerate `migration/route-manifest.json` from Rails routes and verify with `migration/tools/check-manifest.mjs` |

---

## Contentful test seed

| Action | PowerShell | Bash | What it does |
|---|---|---|---|
| **contentful-check** | `.\scripts\migration.ps1 contentful-check` | `bash bin/migration contentful-check` | Offline check of exported schema + generated synthetic Rails seed (no Contentful API) |
| **contentful-seed** | `.\scripts\migration.ps1 contentful-seed` | `bash bin/migration contentful-seed` | Import schema + synthetic fixtures into an **explicitly approved** non-production space (`CONTENTFUL_ALLOW_TEST_SEED=true`) |

> [!danger] Never seed production
> Only non-production spaces/environments. Tokens stay in `.env` / CI secrets — never commit them.

---

## Useful Compose one-offs

Rebuild/recreate one service:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery build gateway
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery up --detach --no-deps gateway
```

Follow logs:

```powershell
podman compose -f docker-compose.yml -f docker-compose.dev.yml -f compose.migration.yml --project-name recovery logs --follow app dotnet gateway otel-collector
```

Podman API bridge (manual session, if wrappers are not used):

```powershell
podman machine ssh podman-machine-default "systemctl --user is-active --quiet podman-migration-api.service || systemd-run --user --unit=podman-migration-api --collect podman system service --time=0 tcp:127.0.0.1:2375"
$env:DOCKER_HOST = "tcp://127.0.0.1:2375"
```

---

## Suggested daily loops

### Characterise Rails / write journeys

1. `.\scripts\migration.ps1 up`
2. `.\scripts\migration.ps1 parity-journey` (or a single file while iterating)
3. Optionally `.\scripts\migration.ps1 parity-journey-gateway` before claiming gateway readiness
4. Headed debug: `.\scripts\migration.ps1 parity-journey-headed`

### After a .NET slice lands

1. Keep Rails as reference; route via YARP only when gates pass (see [[Porting Rails Project To DfE]])
2. Point journeys at gateway / .NET with `TARGET_BASE_URL`
3. Run `parity-test` for harness-level checks
4. Update `migration/route-manifest.json` via **manifest**

### Contentful / Rails green

1. `contentful-check` locally
2. Approved seed → `contentful-seed`
3. `migration-test` / focused `bin/podman-rspec`

---

## Related notes

- Strategy: [[Porting Rails Project To DfE]]
- Prompts: [[Prompts]]
- Agent run journals: [[Logs]]
- Repo operator guide (non-Obsidian): `MIGRATION.md` at repository root
