# Migration status

## Platform

| Capability | State | Evidence |
|---|---|---|
| Rails reference | healthy; full UI/tests require non-production Contentful credentials | Executed `GET localhost:3000/health` returned 200; RSpec credential blocker recorded in `evidence/final-platform-report.md` |
| ASP.NET Core shell | implemented and healthy | `dotnet/src/EarlyYears.Web/Program.cs`; 6 containerized .NET tests passed; `GET localhost:5000/health` returned 200 |
| PostgreSQL connectivity / Rails schema ownership guard | implemented and executed | `dotnet/src/EarlyYears.Infrastructure/DependencyInjection.cs`; unit tests; guarded reset cloned 18-table Rails schema |
| Rails-default YARP gateway | implemented and healthy | `gateway/appsettings.json`; integration tests; gateway `/health` matched Rails `/health` |
| Playwright platform harness | implemented; current validation recorded per run journal | `parity-test` type-checks the complete harness and runs only platform controls; product journeys are a separate suite |
| Route inventory | generated and current | `route-manifest.json` (114 executed Rails routes); stale check passed |
| CI | implemented, not yet observed in GitHub | `.github/workflows/migration-platform.yml` |
| Acceptance evidence enforcement | implemented, no route yet eligible | `migration/tools/check-acceptance-evidence.mjs`; `migration/acceptance-evidence.json` |

## Business migration

No business route is migrated. All application traffic remains on the `rails-catch-all` route. The .NET candidate intentionally returns 404 for application paths.

The database, visual, accessibility and side-effect platform tests prove that the harness controls operate; they are not evidence for a business route. Route-specific evidence must be added to `acceptance-evidence.json` before changing a manifest route to `approved-dotnet`.
