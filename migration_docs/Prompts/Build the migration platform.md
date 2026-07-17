---
title: Build the migration platform
aliases:
  - Starting Prompt
parent: "[[Prompts]]"
tags:
  - prompt
  - rails
  - dotnet
  - migration-platform
---

# Build the migration platform

You are the lead migration engineer responsible for creating a production-grade, AI-assisted Rails-to-ASP.NET Core migration platform for the repository open in this workspace.

The existing DfE Ruby on Rails service must be migrated incrementally while preserving externally observable behaviour and UI fidelity. Build the complete migration platform—not a prototype or MVP. **Do not migrate business features in this task.**

## Objective

Create a deterministic local and CI environment in which:

1. Rails runs as the behavioural reference.
2. A new ASP.NET Core application runs alongside Rails.
3. A YARP gateway sends all traffic to Rails by default and can route individually approved paths to .NET.
4. One independent Playwright suite runs against Rails, .NET and the gateway.
5. HTTP, visual, accessibility, database and side-effect parity can be captured and compared.
6. The stack runs through Podman Compose on Windows with Podman Desktop and WSL2.
7. Every service has health checks, deterministic startup and documented commands.

## Non-negotiable rules

- Rails is the behavioural source of truth, except for a known security defect explicitly classified as behaviour that must not be reproduced.
- Do not redesign the UI or replace its CSS, JavaScript, GOV.UK components, page structure or content presentation.
- Do not change Rails behaviour or dependencies merely to make migration easier.
- Do not infer behaviour from Rails conventions. Cite repository files and line ranges, an executed test or an observed request for every behavioural claim.
- Record unknown behaviour as unknown.
- Do not mark work complete from visual inspection alone.
- Keep the parity harness implementation-independent; do not reuse production comparison logic.
- Rails remains the database-schema owner during coexistence. The .NET application must not apply migrations.
- Never commit credentials, Rails master keys, API keys or production data.
- Use fixtures, simulators, local fakes or test environments for external systems.
- Pin versions where reproducibility depends on them.
- All local commands must use Podman; do not require Ruby or PostgreSQL directly on Windows.

## Required structure

Create or complete:

```text
dotnet/
  EarlyYears.sln
  src/
    EarlyYears.Web/
    EarlyYears.Application/
    EarlyYears.Domain/
    EarlyYears.Infrastructure/
  tests/
    EarlyYears.UnitTests/
    EarlyYears.IntegrationTests/

gateway/
  MigrationGateway.csproj
  Program.cs
  appsettings.json
  Dockerfile

parity/
  package.json
  package-lock.json
  playwright.config.ts
  tests/
    smoke/
    journeys/
    contracts/
    visual/
    accessibility/
    gateway/
  support/
    database/
    http/
    normalisation/
    side-effects/
  fixtures/
  baselines/
  reports/

migration/
  evidence/
  slices/
  route-manifest.json
  risk-register.md
  known-differences.md
  migration-status.md
  acceptance-gates.md

compose.migration.yml
compose.parity.yml
MIGRATION.md
```

Adapt names only where the repository provides a clear existing convention. Explain any deviation.

## Technical requirements

### ASP.NET Core

- Use the current supported .NET LTS version approved for the project.
- Use ASP.NET Core MVC and Razor for server-rendered pages.
- Add a health endpoint and PostgreSQL connectivity against the existing schema.
- Do not scaffold speculative domain abstractions or business features.
- Add structured logging and OpenTelemetry-compatible tracing.
- Safely honour forwarded headers behind YARP.
- Enable nullable reference types, analyzers and warnings-as-errors where practical.
- Provide a multi-stage Dockerfile with development, test and runtime targets.

### YARP gateway

- Create a standalone ASP.NET Core gateway.
- Route all application traffic to Rails by default.
- Define the .NET cluster without sending business routes to it yet.
- Give approved .NET routes higher priority than the Rails catch-all.
- Add a gateway-only health endpoint.
- Preserve host, path, query, method and forwarded request information.
- Add tests proving the catch-all reaches Rails.
- Keep route changes configuration-driven and reviewable.
- Do not expose a public header or query parameter that selects the backend.

### Podman Compose

Extend existing Compose files rather than replacing them. Reuse `app`, `db`, `gov-one-simulator`, `worker`, `otel-collector` and `pa11y` where they exist. Add `dotnet`, `gateway` and profile-gated `parity` services.

Expose:

| Service | Local URL |
|---|---|
| Rails reference | `http://localhost:3000` |
| .NET candidate | `http://localhost:5000` |
| YARP gateway | `http://localhost:8080` |

All services must use the existing recovery network. Add health checks, explicit dependencies, named package-cache volumes, environment-variable documentation and Windows-compatible commands. Use polling file watchers where Windows bind mounts make native watching unreliable. Ordinary development must not start parity services automatically.

### Playwright parity harness

Use Playwright with TypeScript. Configure:

```text
RAILS_BASE_URL
DOTNET_BASE_URL
GATEWAY_BASE_URL
```

Add initial tests proving that:

1. Rails, .NET and YARP are reachable.
2. the gateway catch-all routes to Rails;
3. deterministic screenshots can be captured in a container;
4. status, redirects, selected headers, cookies and HTML can be recorded;
5. accessibility scanning executes; and
6. traces, screenshots and video are retained on failure.

Create reusable fixtures for target selection, anonymous browser context, an authenticated-context placeholder, HTTP capture, screenshot normalisation, justified dynamic-value masking, database reset/state capture and side-effect capture.

Do not weaken visual checks with broad exclusions or large tolerances. Every mask or normalisation needs a written reason.

### Database comparison

Provide infrastructure that performs this sequence:

1. restore a canonical fixture;
2. run a scenario against Rails;
3. capture selected database state as canonical JSON;
4. restore the same fixture;
5. run the scenario against .NET;
6. capture the same state; and
7. compare the captures, ignoring only approved volatile fields.

Run write scenarios sequentially. Never allow both applications to mutate the comparison database concurrently.

## Application inventory

Analyse the Rails repository and generate `migration/route-manifest.json`. For every route record:

- HTTP method and path;
- Rails controller and action;
- authentication and authorisation requirements;
- primary view or response type;
- models read and written;
- external services and background jobs;
- existing request/system coverage;
- current migration state;
- evidence references;
- confidence in the evidence; and
- unresolved questions.

Generate `migration/risk-register.md` covering at least GOV.UK One Login, sessions and cookies, Contentful, GOV.UK Notify, background and scheduled jobs, PDF/certificate generation, analytics and exports, accessibility, caching, time zones, database callbacks and constraints, observability, CSP/security headers, error handling, redirects and Contentful request multiplication.

## Documentation and commands

`MIGRATION.md` must explain prerequisites, WSL2 and Podman machine setup, Compose installation, startup, targeted rebuilds, Rails/.NET/parity tests, visual-baseline updates, database resets, Playwright traces, logs, worker enablement, route migration and rollback, plus Windows bind-mount, VPN and port-conflict troubleshooting.

Create cross-platform scripts or clearly documented commands for:

```text
migration-up
migration-down
migration-build
migration-test
parity-test
parity-update-baselines
parity-reset
parity-report
```

## CI requirements

Add a workflow that:

1. builds Rails and .NET images;
2. runs existing Rails tests and .NET unit/integration tests;
3. starts deterministic parity services;
4. runs smoke and gateway tests;
5. uploads Playwright traces, screenshots and reports on failure;
6. fails when generated migration manifests are stale; and
7. uses no production credentials.

## Working order

1. Inspect the repository, Compose files and scripts.
2. Produce an evidence-backed implementation plan.
3. Create the repository structure and .NET shell.
4. Implement YARP and Compose integration.
5. Implement the parity harness.
6. Generate the route inventory and risk register.
7. Add scripts, documentation and CI.
8. Build and run the complete environment and tests.
9. Fix failures and produce the evidence report.

Do not stop after creating files. Execute the stack and tests wherever the available tools permit.

## Required final report

Report:

- files created and modified;
- commands executed and exact reproduction steps;
- test results and service URLs;
- route counts and coverage gaps;
- assumptions and unresolved blockers; and
- every step that could not be executed.

Do not claim success unless the complete stack builds, starts and the initial parity tests pass. If tooling or access prevents that, report the exact blocker and leave the result explicitly incomplete.
