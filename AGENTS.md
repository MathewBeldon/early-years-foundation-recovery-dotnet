# Repository Guidelines

## Project Structure & Module Organization

Rails uses `app/` for source, `config/` for configuration, `db/` for schema, and `spec/` for RSpec. The replacement is under `dotnet/`, with tests in `dotnet/tests/`. `gateway/` contains YARP, `parity/` contains Playwright, and `migration/` holds evidence, route ownership, risks, and gates. Extend Compose files; do not replace the Rails stack.

## Build, Test, and Development Commands

Use Podman Desktop with WSL2. Run Bash wrappers from Git Bash or use PowerShell equivalents:

- `bash bin/migration-up` / `.\scripts\migration.ps1 up`: build and start Rails, .NET, YARP, PostgreSQL, One Login, and telemetry.
- `bash bin/migration-test`: build and run Rails and .NET tests. Full Rails tests require non-production `CONTENTFUL_TEST_*` values.
- `bash bin/parity-test`: run the pinned Playwright suite.
- `bash bin/parity-journey` / `.\scripts\migration.ps1 parity-journey`: run standalone registration (and later product) journeys against Rails (`TARGET_BASE_URL`).
- `bash bin/parity-journey-gateway` / `.\scripts\migration.ps1 parity-journey-gateway`: same journey suite through YARP (`http://gateway.recovery.internal:8080`).
- `bash bin/parity-journey-headed` / `.\scripts\migration.ps1 parity-journey-headed`: visible host Chromium against Rails (`localhost:3000`).
- `bash bin/parity-journey-gateway-headed` / `.\scripts\migration.ps1 parity-journey-gateway-headed`: visible host Chromium through YARP (`localhost:8080`).
- `bash bin/parity-update-baselines`: deliberately regenerate exact-pixel baselines.
- `bash bin/migration-manifest`: regenerate and verify `migration/route-manifest.json`.
- `bash bin/migration contentful-check`: verify the authoritative Contentful schema export and generated synthetic Rails seed without contacting Contentful.
- `bash bin/migration contentful-seed`: import the exact exported model and synthetic Rails fixtures into an explicitly approved non-production Contentful space.

See `MIGRATION.md` for database resets, reports, logs, VPN, and Windows troubleshooting.

## Coding Style & Naming Conventions

Ruby follows `rubocop-govuk`, two-space indentation, single quotes, and `snake_case`. SCSS uses `stylelint-config-gds`. TypeScript is strict and uses `.spec.ts` tests. C# enables nullable references, analyzers, central package versions, and warnings as errors; use PascalCase types/methods and camelCase locals. Do not scaffold speculative domain abstractions.

## Testing Guidelines

Rails uses RSpec/RackTest; .NET uses xUnit; parity uses Playwright, Axe, and exact screenshots. Add focused tests beside the affected layer. Never run Rails and .NET write comparisons concurrently against the same database. Dynamic screenshot masks and volatile database fields require explicit justification. Rails remains the behavioural source of truth.

## Commit & Pull Request Guidelines

History favours short imperative subjects, often prefixed with a ticket such as `EYCDTK-1373`. Keep commits scoped. PRs should explain behaviour and risk, link the ticket, list tests, and include screenshots or parity evidence for UI changes. Identify migrations, configuration changes, and known differences.

## Security & Configuration

Never commit Rails keys, production data, API tokens, or credentials. Use `.env`, CI secrets, simulators, synthetic fixtures, and non-production Contentful environments. Rails alone owns schema migrations during coexistence; .NET must not apply them.

## Agent Run Journal

Before the final response for any run that changes files or executes validation, create `migration_docs/Logs/YYYY-MM-DD-HHMM-<slug>.md`. Record Europe/London start and end times, elapsed wall-clock time (label estimates), objective, summary, files changed, commands/tests and outcomes, decisions, blockers, and next action. Log failed or blocked runs too. Use one new file per run; never overwrite history or include secrets, credentials, production data, or noisy raw output.

Obsidian vault notes under `migration_docs/` (for example [[Porting Rails Project To DfE]], [[Migration platform commands]], [[Prompts]]) must link to the [[Logs]] hub note — not only to the `Logs/` folder path as plain text. When creating a new journal file, also prepend a wikilink bullet under **Journal index** in `migration_docs/Logs.md` (newest first).
