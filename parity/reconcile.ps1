$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$compose = Join-Path $PSScriptRoot "docker-compose.yml"
$containerEngine = if ($env:CONTAINER_ENGINE) { $env:CONTAINER_ENGINE } else { "podman" }
$sql = @"
SELECT json_build_object(
  'users', (SELECT json_build_object('count', count(*), 'min_id', min(id), 'max_id', max(id)) FROM users),
  'progress', (SELECT json_build_object('count', count(*), 'completed', count(completed_at)) FROM user_module_progress),
  'assessments', (SELECT json_build_object('count', count(*), 'passed', count(*) FILTER (WHERE passed)) FROM assessments),
  'responses', (SELECT json_build_object('count', count(*), 'correct', count(*) FILTER (WHERE correct)) FROM responses),
  'notes', (SELECT json_build_object('count', count(*), 'min_id', min(id), 'max_id', max(id)) FROM notes),
  'visits', (SELECT count(*) FROM visits),
  'events', (SELECT count(*) FROM events),
  'mail_events', (SELECT count(*) FROM mail_events)
)::text;
"@

function Snapshot([string]$service, [string]$database) {
    if ($containerEngine -eq "podman") {
        $value = python -m podman_compose -f $compose exec -T $service psql -U postgres -d $database -Atc $sql
    } else {
        $value = docker compose -f $compose exec -T $service psql -U postgres -d $database -Atc $sql
    }
    if ($LASTEXITCODE -ne 0) { throw "Could not snapshot $database" }
    return ($value | ConvertFrom-Json)
}

$rails = Snapshot "rails-db" "rails_parity"
$dotnet = Snapshot "dotnet-db" "dotnet_parity"
$railsJson = $rails | ConvertTo-Json -Depth 8 -Compress
$dotnetJson = $dotnet | ConvertTo-Json -Depth 8 -Compress
$result = [ordered]@{ rails = $rails; dotnet = $dotnet; matches = ($railsJson -eq $dotnetJson) }
$reportDirectory = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force $reportDirectory | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $reportDirectory "database-reconciliation.json")
if (-not $result.matches) { throw "Rails/.NET database reconciliation differs. See TestResults/database-reconciliation.json" }
Write-Host "Database reconciliation passed."
