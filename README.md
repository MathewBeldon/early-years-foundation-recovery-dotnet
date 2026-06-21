# Early Years Foundation Recovery (.NET)

## Prerequisites

- .NET 10 SDK
- [Podman Desktop](https://podman-desktop.io/) (or Podman CLI) with Compose support, with the Podman machine running

## Start

```powershell
# 1. Start dependencies: PostgreSQL (5433) + GOV.UK One Login simulator (3333)
podman compose up -d
# or: .\compose.ps1 up -d

# 2. Run the app
dotnet run --project src/EarlyYearsFoundationRecovery.Web
```

- App: http://localhost:5000
- Health: http://localhost:5000/health
- One Login simulator: http://localhost:3333

If Podman is not running, the app will fail to start in Development because it cannot connect to PostgreSQL.

## Stop

```powershell
podman compose down       # stop containers
podman compose down -v    # also remove volumes
```
