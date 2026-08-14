param([ValidateSet("up", "down", "reset", "test")][string]$Command = "up")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$railsSource = Join-Path $root "parity/.rails-source"
$compose = Join-Path $root "parity/docker-compose.yml"
$containerEngine = if ($env:CONTAINER_ENGINE) { $env:CONTAINER_ENGINE } else {
    podman info --format '{{.Host.OS}}' 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { "podman" } else { "docker" }
}
function Invoke-Compose([string[]]$ComposeArgs) {
    if ($containerEngine -eq "podman") { & python -m podman_compose @ComposeArgs }
    else { & docker compose @ComposeArgs }
    if ($LASTEXITCODE -ne 0) { throw "$containerEngine compose failed with exit code $LASTEXITCODE" }
}

if ($Command -in @("up", "reset")) {
    if (-not (Test-Path $railsSource)) {
        git -c safe.directory=$root worktree add --detach $railsSource 33c73750
    }
    $railsPatch = Join-Path $root "parity/rails-protocol-fakes.patch"
    $patchedFile = git -C $railsSource status --porcelain -- config/initializers/contentful_rails.rb
    if (-not $patchedFile) { git -C $railsSource apply $railsPatch }
    $railsAuthPatch = Join-Path $root "parity/rails-auth-fakes.patch"
    $authPatchedFile = git -C $railsSource status --porcelain -- config/application.rb
    if (-not $authPatchedFile) { git -C $railsSource apply $railsAuthPatch }
}

if ($Command -eq "up") {
    Invoke-Compose @("-f", $compose, "up", "-d", "--build")
} elseif ($Command -eq "down") {
    Invoke-Compose @("-f", $compose, "down")
} elseif ($Command -eq "reset") {
    Invoke-Compose @("-f", $compose, "down", "-v")
    Invoke-Compose @("-f", $compose, "up", "-d", "--build")
} else {
    $env:RAILS_BASE_URL = "http://localhost:3000"
    $env:DOTNET_BASE_URL = "http://localhost:5000"
    dotnet test (Join-Path $root "src/EarlyYearsFoundationRecovery.ParityTests")
    $parityExitCode = $LASTEXITCODE
    & (Join-Path $root "parity/reconcile.ps1")
    $reconciliationExitCode = $LASTEXITCODE
    if ($parityExitCode -ne 0) { throw "Parity tests failed with exit code $parityExitCode" }
    if ($reconciliationExitCode -ne 0) { throw "Database reconciliation failed with exit code $reconciliationExitCode" }
}
