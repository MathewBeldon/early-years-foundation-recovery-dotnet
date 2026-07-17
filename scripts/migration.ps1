[CmdletBinding()]
param(
  [Parameter(Mandatory = $true, Position = 0)]
  [ValidateSet('up', 'down', 'build', 'test', 'validate', 'parity-test', 'parity-journey', 'parity-journey-gateway', 'parity-journey-headed', 'parity-journey-gateway-headed', 'parity-update-baselines', 'parity-reset', 'parity-report', 'manifest', 'contentful-check', 'contentful-seed')]
  [string]$Action,

  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$RemainingArguments
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force @(
  'tmp/parity/test-results',
  'tmp/parity/reports',
  'tmp/parity/side-effects'
) | Out-Null

if ($env:OS -eq 'Windows_NT' -and -not $env:DOCKER_HOST) {
  & podman machine ssh podman-machine-default `
    'systemctl --user is-active --quiet podman-migration-api.service || systemd-run --user --unit=podman-migration-api --collect podman system service --time=0 tcp:127.0.0.1:2375'
  if ($LASTEXITCODE -ne 0) {
    throw 'Could not initialise the localhost-only Podman Compose API bridge.'
  }
  $env:DOCKER_HOST = 'tcp://127.0.0.1:2375'
}
$migrationCompose = @(
  'compose',
  '-f', 'docker-compose.yml',
  '-f', 'docker-compose.dev.yml',
  '-f', 'compose.migration.yml',
  '--project-name', 'recovery'
)
$parityCompose = $migrationCompose + @('-f', 'compose.parity.yml', '--profile', 'parity')
$journeysCompose = $migrationCompose + @('-f', 'compose.parity.yml', '--profile', 'journeys')
$journeysGatewayCompose = $migrationCompose + @('-f', 'compose.parity.yml', '--profile', 'journeys-gateway')

function Invoke-Podman([string[]]$Arguments) {
  & podman @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "podman failed with exit code $LASTEXITCODE"
  }
}

function Invoke-JourneyRunner([string[]]$ComposeArgs, [string]$ServiceName) {
  # A leading test path replaces the defaults; option-only args modify them, e.g.:
  #   .\scripts\migration.ps1 parity-journey tests/journeys/registration.spec.ts
  #   .\scripts\migration.ps1 parity-journey tests/journeys/my-modules.spec.ts --workers=2
  #   .\scripts\migration.ps1 parity-journey -g "clears local authority"
  $envArgs = @()
  if ($RemainingArguments -and $RemainingArguments.Count -gt 0) {
    $joined = [string]::Join([char]0, $RemainingArguments)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($joined))
    # Base64 preserves the argument boundary without shell evaluation in the container.
    $env:PLAYWRIGHT_ARGS_BASE64 = $encoded
    $envArgs += @('--env', "PLAYWRIGHT_ARGS_BASE64=$encoded")
  }
  if ($env:PLAYWRIGHT_WORKERS) {
    $envArgs += @('--env', "PLAYWRIGHT_WORKERS=$($env:PLAYWRIGHT_WORKERS)")
  }
  Invoke-Podman ($ComposeArgs + @('run', '--rm') + $envArgs + @($ServiceName))
}

function Invoke-HeadedJourneys([string]$DefaultTargetBaseUrl) {
  if (-not $env:TARGET_BASE_URL) { $env:TARGET_BASE_URL = $DefaultTargetBaseUrl }
  if (-not $env:GOV_ONE_SIMULATOR_URL) { $env:GOV_ONE_SIMULATOR_URL = 'http://localhost:4000' }
  # Rails authorize links use http://gov-one-simulator:4000; map that hostname in Chromium.
  $env:PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR = '1'
  if (-not $env:PLAYWRIGHT_SLOW_MO) { $env:PLAYWRIGHT_SLOW_MO = '500' }
  if (-not $env:PLAYWRIGHT_TEST_TIMEOUT) { $env:PLAYWRIGHT_TEST_TIMEOUT = '240000' }

  foreach ($probe in @(
    @{ Name = 'Application'; Url = "$($env:TARGET_BASE_URL.TrimEnd('/'))/health" },
    @{ Name = 'One Login simulator'; Url = $env:GOV_ONE_SIMULATOR_URL.TrimEnd('/') }
  )) {
    try {
      $response = Invoke-WebRequest -Uri $probe.Url -UseBasicParsing -TimeoutSec 5
      if ($response.StatusCode -ge 400) {
        throw "HTTP $($response.StatusCode)"
      }
    }
    catch {
      throw @"
$($probe.Name) is not reachable at $($probe.Url) ($($_.Exception.Message)).
Start the stack first: .\scripts\migration.ps1 up
"@
    }
  }

  Push-Location parity
  try {
    if (-not (Test-Path 'node_modules')) {
      & npm ci --prefer-offline --no-audit
      if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }
    & npx playwright install chromium
    if ($LASTEXITCODE -ne 0) { throw 'playwright install chromium failed.' }
      $headedArgs = @('playwright', 'test', 'tests/journeys', 'tests/concurrency', '--headed') + @($RemainingArguments)
      & npx @headedArgs
      if ($LASTEXITCODE -ne 0) { throw "headed journeys failed with exit code $LASTEXITCODE" }
  }
  finally {
    Pop-Location
  }
}

switch ($Action) {
  'validate' {
    $gitBash = Join-Path $env:ProgramFiles 'Git\bin\bash.exe'
    if (-not (Test-Path -LiteralPath $gitBash)) {
      throw "Git Bash was not found at $gitBash. Install Git for Windows or run bin/migration-validate from an existing Git Bash shell."
    }
    & $gitBash bin/migration-validate @RemainingArguments
    if ($LASTEXITCODE -ne 0) { throw "migration validation failed with exit code $LASTEXITCODE" }
  }
  'up' {
    Invoke-Podman ($migrationCompose + @('up', '--build', '--detach', 'app', 'gov-one-simulator', 'otel-collector', 'dotnet', 'gateway') + $RemainingArguments)
  }
  'down' {
    Invoke-Podman ($parityCompose + @('down', '--remove-orphans') + $RemainingArguments)
  }
  'build' {
    Invoke-Podman ($migrationCompose + @('build', 'app', 'dotnet', 'gateway') + $RemainingArguments)
  }
  'test' {
    Invoke-Podman @(
      'compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.test.yml',
      '--project-name', 'recovery', 'build', 'app'
    )
    Invoke-Podman @(
      'compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.test.yml',
      '--project-name', 'recovery', 'run', '--rm', 'app', 'rails', 'db:drop', 'db:create', 'db:migrate'
    )
    Invoke-Podman @(
      'compose', '-f', 'docker-compose.yml', '-f', 'docker-compose.test.yml',
      '--project-name', 'recovery', 'run', '--rm', 'app', 'rspec'
    )
    Invoke-Podman (@('build', '--file', 'dotnet/Dockerfile', '--target', 'test', '--tag', 'recovery-dotnet:test', '.') + $RemainingArguments)
  }
  'parity-test' {
    Invoke-Podman ($parityCompose + @('run', '--rm', 'parity') + $RemainingArguments)
  }
  'parity-journey' {
    Invoke-JourneyRunner $journeysCompose 'parity-journeys'
  }
  'parity-journey-gateway' {
    Invoke-JourneyRunner $journeysGatewayCompose 'parity-journeys-gateway'
  }
  'parity-journey-headed' {
    Invoke-HeadedJourneys 'http://localhost:3000'
  }
  'parity-journey-gateway-headed' {
    Invoke-HeadedJourneys 'http://localhost:8080'
  }
  'parity-update-baselines' {
    Invoke-Podman ($parityCompose + @('run', '--rm', 'parity', 'bash', '-lc', 'npm ci --prefer-offline --no-audit && npx playwright test tests/visual --update-snapshots'))
  }
  'parity-reset' {
    Invoke-Podman ($parityCompose + @('run', '--rm', '--env', 'PARITY_ALLOW_DATABASE_RESET=true', 'parity', 'bash', '-lc', 'npm ci --prefer-offline --no-audit && npx playwright test tests/database/reset-execute.spec.ts'))
  }
  'parity-report' {
    Invoke-Podman ($parityCompose + @('run', '--rm', '--service-ports', 'parity', 'bash', '-lc', 'npm ci --prefer-offline --no-audit && npx playwright show-report reports/playwright-report --host 0.0.0.0'))
  }
  'manifest' {
    # Compose supplies the migration-safe Rails environment and source mount.
    Invoke-Podman ($migrationCompose + @(
      'run', '--rm', '--no-deps',
      '--env', 'RAILS_ENV=test',
      '--env', 'ENVIRONMENT=test',
      '--env', 'RAILS_MASTER_KEY=',
      '--env', 'MIGRATION_SKIP_CREDENTIALS=true',
      '--env', 'SECRET_KEY_BASE=migration-manifest-not-for-production',
      'app',
      'bundle', 'exec', 'rails', 'runner', 'migration/tools/generate-route-manifest.rb'
    ))
    & node migration/tools/check-manifest.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Route manifest check failed.' }
  }
  'contentful-check' {
    Invoke-Podman ($migrationCompose + @('--profile', 'contentful-tools', 'build', 'contentful-seed'))
    Invoke-Podman ($migrationCompose + @('--profile', 'contentful-tools', 'run', '--rm', 'contentful-seed', 'npm', 'run', 'check'))
  }
  'contentful-seed' {
    Invoke-Podman ($migrationCompose + @('--profile', 'contentful-tools', 'build', 'contentful-seed'))
    Invoke-Podman ($migrationCompose + @('--profile', 'contentful-tools', 'run', '--rm', '--env', 'CONTENTFUL_ALLOW_TEST_SEED=true', 'contentful-seed', 'npm', 'run', 'seed'))
  }
}
