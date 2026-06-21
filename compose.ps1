# Run Podman Compose against the .NET dependencies only (Postgres + One Login simulator).
# Usage: .\compose.ps1 up -d

$ComposeFile = Join-Path $PSScriptRoot "docker-compose.yml"

podman compose -f $ComposeFile @args
