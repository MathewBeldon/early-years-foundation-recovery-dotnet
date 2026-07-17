# Migration platform implementation plan

This plan deliberately stops before business-feature migration. Rails remains the behavioural reference and YARP remains a Rails catch-all.

## Evidence baseline

- Rails 7.2.3.1 and Ruby 3.4.5 are locked in `Gemfile.lock:529-531` and `Gemfile.lock:822-826`.
- Rails, PostgreSQL and the recovery network already exist in `docker-compose.yml:1-47`; development adds One Login, the worker and Pa11y in `docker-compose.dev.yml:1-142`.
- Existing Podman entry points are Bash scripts in `bin/podman-dev:1-13` and `bin/podman-rspec:1-28`.
- The application route DSL is in `config/routes.rb:1-85`; the generated manifest is produced from the executed Rails route set, not from convention alone.
- Rails system tests use RackTest (`README.md:75-79`), while cookie behaviour already has system coverage in `spec/system/cookies_spec.rb:1-62`.
- Rails owns a PostgreSQL schema with foreign keys in `db/schema.rb:259-268` and Que tables in `db/schema.rb:70-138`.
- Authentication uses Devise/OpenID Connect (`app/models/user.rb:52-58`, `config/initializers/devise.rb:271-289`) and Contentful is selected per request (`app/controllers/application_controller.rb:48-55`).

## Delivery order

1. Pin .NET, NuGet, Playwright and migration container versions; preserve Rails versions.
2. Create empty Domain/Application projects, a connectivity-only Infrastructure project, an MVC/Razor Web shell and tests. Do not create speculative entities or EF migrations.
3. Create a standalone YARP gateway with a gateway-owned health endpoint and only the Rails catch-all active.
4. Extend the existing Compose graph with health-gated .NET and gateway services; keep parity behind a profile.
5. Add an implementation-independent Playwright harness with exact visual comparison, axe, HTTP capture, database reset/capture and a local side-effect sink.
6. Generate a source-digested route inventory from `Rails.application.routes.routes`, then maintain explicit risks, differences and acceptance gates.
7. Add Git Bash and PowerShell commands plus a Podman-based CI workflow.
8. Build all images, run Rails/.NET/parity tests, retain evidence, and report any host-level blocker without claiming success.

## Pre-migration parity hardening

The platform-scaffolding order above does not authorize business migration. Before selecting or implementing the first authenticated boundary:

1. complete the Rails session characterization in `migration/session-ownership-plan.md` before ASP.NET Core session design begins;
2. inventory pre-authentication, authenticated, workflow-nonce and framework session purposes rather than treating the session as an identity cookie only;
3. approve one owner for the complete session-coupled boundary and document cutover and rollback behaviour;
4. correct the contract gate so runtime-specific session representation can differ only through an approved ownership record while observable security semantics remain gated;
5. keep `KD-SESSION-001` proposed until executed Rails evidence and named security/architecture approval exist; and
6. keep all authenticated routes on Rails until the session plan exit criteria and every applicable acceptance gate pass.

Anonymous feedback/Ahoy attribution is a distinct cookie-ownership decision and must receive its own record if that boundary is proposed separately.

## Key design decisions

- .NET 10 LTS is pinned because the approved SDK is present and it is supported through November 2028.
- Npgsql connectivity is registered directly. EF Core is intentionally absent, preventing accidental migration generation/application before a mapped slice requires it.
- `/health` is liveness-only; `/health/ready` probes PostgreSQL. Compose uses liveness so a database outage is visible without creating restart loops.
- YARP route changes live only in `gateway/appsettings.json`; no request header can select a backend.
- Write comparisons use a dedicated `_parity` database, clone a Rails-owned template, hold a deterministic advisory lock, and execute Rails then .NET sequentially.
- Write captures include identity/account state, Ahoy `events`, `mail_events`, and every current learner-write table (`assessments`, `confidence_check_progress`, `notes`, `responses`, and `user_module_progress`). Timestamp fields are retained in the capture plan but projected separately from exact database equality; their comparator checks presence, ordering and declared windows.
- Visual comparison allows zero differing pixels. Any masked selector must carry a written justification in `parity/fixtures/dynamic-regions.json`.
