param([ValidateSet("up", "down", "reset", "test", "check-pin")][string]$Command = "up")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$railsSource = Join-Path $root "parity/.rails-source"
$compose = Join-Path $root "parity/docker-compose.yml"

# Exit codes distinguish "the contract needs a human decision" from "the tooling
# could not run", so automation can treat them differently.
$ExitPinReviewRequired = 3
$ExitOperationalFailure = 4
$ExitContractMismatch = 5

# parity/rails-contract.json is the single source of truth for the pin. No commit
# or tag is hard-coded here; RailsContractConsistencyTests enforces that.
$contractPath = Join-Path $root "parity/rails-contract.json"
if (-not (Test-Path $contractPath)) { throw "Missing contract manifest at $contractPath." }
$contract = Get-Content $contractPath -Raw | ConvertFrom-Json

if ($contract.commit -notmatch '^[0-9a-f]{40}$') {
    throw "rails-contract.json: 'commit' must be a full 40-character SHA, found '$($contract.commit)'."
}
if ($contract.schemaVersion -notmatch '^\d{14}$') {
    throw "rails-contract.json: 'schemaVersion' must be 14 digits, found '$($contract.schemaVersion)'."
}
if ($contract.reviewedOn -notmatch '^\d{4}-\d{2}-\d{2}$') {
    throw "rails-contract.json: 'reviewedOn' must be ISO YYYY-MM-DD, found '$($contract.reviewedOn)'."
}
if ([string]::IsNullOrWhiteSpace($contract.releaseRef)) {
    throw "rails-contract.json: 'releaseRef' must not be empty."
}

# Kept identical to the message in RailsContractConsistencyTests so both routes
# to this failure hand back the same remediation.
$refreshWorktreeCommand = "git worktree remove --force parity/.rails-source"

# Resolved on first use, not at load: read-only commands such as check-pin must
# not require a container runtime, and probing podman when none is reachable
# writes to stderr, which is terminating under ErrorActionPreference = Stop.
$script:containerEngine = $null
function Get-ContainerEngine {
    if ($script:containerEngine) { return $script:containerEngine }
    if ($env:CONTAINER_ENGINE) {
        $script:containerEngine = $env:CONTAINER_ENGINE
        return $script:containerEngine
    }
    $engine = "docker"
    $previous = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        podman info --format '{{.Host.OS}}' *> $null
        if ($LASTEXITCODE -eq 0) { $engine = "podman" }
    } catch {
        # podman absent or unreachable; docker is the fallback.
    } finally {
        $ErrorActionPreference = $previous
    }
    $script:containerEngine = $engine
    return $script:containerEngine
}

function Invoke-Compose([string[]]$ComposeArgs) {
    $engine = Get-ContainerEngine
    if ($engine -eq "podman") { & python -m podman_compose @ComposeArgs }
    else { & docker compose @ComposeArgs }
    if ($LASTEXITCODE -ne 0) { throw "$engine compose failed with exit code $LASTEXITCODE" }
}

function Invoke-ResetDown {
    try {
        Invoke-Compose @("-f", $compose, "down", "-v")
    } catch {
        # podman-compose exits non-zero on a clean machine because the expected
        # service containers do not exist. That is a successful reset only when
        # no parity containers or volumes remain; any partial teardown still
        # fails loudly rather than starting on stale state.
        $engine = Get-ContainerEngine
        if ($engine -eq "podman") {
            $containers = @(podman ps -a --format '{{.Names}}' | Where-Object { $_ -like 'parity_*' })
            $volumes = @(podman volume ls --format '{{.Name}}' | Where-Object { $_ -like 'parity_*' })
            if ($containers.Count -eq 0 -and $volumes.Count -eq 0) {
                Write-Host "No parity containers or volumes existed; continuing with a clean reset."
                return
            }
        }

        throw
    }
}

function Get-ComposeServiceContainer([string]$Service) {
    $engine = Get-ContainerEngine
    if ($engine -eq "podman") {
        # podman-compose 1.6 cannot filter `ps` by service. Its project and
        # service naming is deterministic for this compose file.
        return "parity_${Service}_1"
    }

    $container = (& docker compose -f $compose ps -q $Service 2>$null | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($container)) {
        throw "Compose did not create a container for service $Service."
    }
    return $container.Trim()
}

function Wait-ContainerLog(
    [string]$Name,
    [string]$Service,
    [string]$Pattern,
    [int]$TimeoutSeconds = 90) {
    $container = Get-ComposeServiceContainer $Service
    $engine = Get-ContainerEngine
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $previous = $ErrorActionPreference
        $logs = $null
        try {
            $ErrorActionPreference = "Continue"
            if ($engine -eq "podman") { $logs = & podman logs $container 2>&1 }
            else { $logs = & docker logs $container 2>&1 }
        } catch {
            # The container can exist before its application has emitted the
            # startup marker. Retry without making an HTTP request: Rails/Ahoy
            # would persist readiness probes as visits and pollute reconciliation.
        } finally {
            $ErrorActionPreference = $previous
        }

        if (($logs -join "`n") -match $Pattern) {
            Write-Host "$Name reported ready."
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "$Name did not report readiness within $TimeoutSeconds seconds."
}

function Wait-ParityEnvironment {
    Wait-ContainerLog "Rails" "rails-app" 'Listening on http://0\.0\.0\.0:3000'
    Wait-ContainerLog ".NET" "dotnet-app" 'Now listening on:\s+http://0\.0\.0\.0:5000'
    Wait-ContainerLog "One Login simulator" "gov-one-login-simulator" 'Server is running at http://localhost:3000'
}

# Upstream tag casing is inconsistent (one historical V1.1.0 among lowercase
# tags), and the leading character is guaranteed by the caller's regex.
function ConvertTo-ReleaseVersion([string]$Tag) { return [version]$Tag.Substring(1) }

# Stable releases only. Release candidates are never selected automatically.
function Get-StableReleaseTags([string]$Remote) {
    # A native command writing to stderr is terminating under
    # ErrorActionPreference = Stop, which would escape as a crash rather than
    # the operational-failure exit code this function exists to report.
    $previous = $ErrorActionPreference
    $output = $null
    try {
        $ErrorActionPreference = "Continue"
        $output = git ls-remote --tags $Remote 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
    } catch {
        return $null
    } finally {
        $ErrorActionPreference = $previous
    }

    $tags = @{}
    foreach ($line in $output) {
        if ($line -notmatch '^([0-9a-f]{40})\s+refs/tags/(v\d+\.\d+\.\d+)(\^\{\})?$') { continue }
        $sha = $Matches[1]
        $tag = $Matches[2]
        # A dereferenced (^{}) entry names the commit an annotated tag points at,
        # so it always wins over the tag object's own hash.
        if ($Matches[3] -or -not $tags.ContainsKey($tag)) { $tags[$tag] = $sha }
    }
    return $tags
}

# Every git call that reads this repository or its linked worktree goes through
# here. The checkout may be owned by a different account than the one running,
# and git then refuses with "dubious ownership". Omitting the guard on a single
# call made a contract mismatch report as a retryable operational failure, so it
# is applied centrally rather than per call site. Calls that take a URL and need
# no repository (ls-remote) deliberately do not use this.
function Invoke-RepoGit { & git -c "safe.directory=$root" @args }

function Get-GitFileAtCommit([string]$Commit, [string]$Path) {
    $previous = $ErrorActionPreference
    $output = $null
    try {
        $ErrorActionPreference = "Continue"
        $output = Invoke-RepoGit show "${Commit}:${Path}" 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
    } catch {
        return $null
    } finally {
        $ErrorActionPreference = $previous
    }

    # A one-line native-command result is a string rather than an array.
    return @($output)
}

if ($Command -eq "check-pin") {
    Write-Host "Pinned: $($contract.releaseRef) ($($contract.commit)), schema $($contract.schemaVersion), reviewed $($contract.reviewedOn)"
    $reviewedOn = [datetime]::ParseExact($contract.reviewedOn, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
    $reviewAge = ((Get-Date).Date - $reviewedOn.Date).Days
    Write-Host "Pin was last reviewed $reviewAge days ago."
    $tags = Get-StableReleaseTags $contract.upstream
    if ($null -eq $tags) {
        Write-Host "Could not reach $($contract.upstream). Pin currency is unverified."
        exit $ExitOperationalFailure
    }
    if (-not $tags.ContainsKey($contract.releaseRef)) {
        Write-Host "Contract mismatch: pinned release $($contract.releaseRef) is not a stable tag upstream. Review parity/rails-contract.json; retrying will not repair the contract."
        exit $ExitContractMismatch
    }
    if ($tags[$contract.releaseRef] -ne $contract.commit) {
        Write-Host "Contract mismatch: pinned release $($contract.releaseRef) resolves upstream to $($tags[$contract.releaseRef]) but the manifest records $($contract.commit). Review parity/rails-contract.json; retrying will not repair the contract."
        exit $ExitContractMismatch
    }

    $schemaLines = Get-GitFileAtCommit $contract.commit "db/schema.rb"
    if ($null -eq $schemaLines) {
        Write-Host "Could not read db/schema.rb at recorded commit $($contract.commit). Required upstream objects are unavailable."
        exit $ExitOperationalFailure
    }
    $schemaMatch = [regex]::Match(($schemaLines -join "`n"), 'define\(version:\s*([0-9_]+)\s*\)')
    $recordedSchema = if ($schemaMatch.Success) { $schemaMatch.Groups[1].Value.Replace("_", "") } else { "<none>" }
    if ($recordedSchema -ne $contract.schemaVersion) {
        Write-Host "Contract mismatch: db/schema.rb at $($contract.commit) declares schema $recordedSchema but the manifest records $($contract.schemaVersion)."
        exit $ExitContractMismatch
    }

    $pinnedVersion = ConvertTo-ReleaseVersion $contract.releaseRef
    # @() keeps a single match an array; otherwise $newer[-1] indexes the string.
    $newer = @($tags.Keys |
        Where-Object { (ConvertTo-ReleaseVersion $_) -gt $pinnedVersion } |
        Sort-Object { ConvertTo-ReleaseVersion $_ })
    if ($newer) {
        Write-Host ""
        Write-Host "Newer stable release(s) available: $($newer -join ', ')"
        Write-Host "Latest is $($newer[-1]) ($($tags[$newer[-1]]))."
        Write-Host "Advancing the pin is a schema-contract change. Review it with the fixture manifest, then update parity/rails-contract.json and RequiredRailsVersion together."
        exit $ExitPinReviewRequired
    }

    Write-Host "$($contract.releaseRef) is the latest stable release. Pin is current."
    exit 0
}

if ($Command -in @("up", "reset")) {
    if (-not (Test-Path $railsSource)) {
        Invoke-RepoGit worktree add --detach $railsSource $contract.commit
    } else {
        # An existing worktree left at an older pin would silently compare .NET
        # against a stale Rails, so require an explicit removal instead.
        $actual = Invoke-RepoGit -C $railsSource rev-parse HEAD
        if ($actual -ne $contract.commit) {
            throw "parity/.rails-source is at $actual but the contract pins $($contract.releaseRef) ($($contract.commit)). Run '$refreshWorktreeCommand' and rerun."
        }
    }
    $railsPatch = Join-Path $root "parity/rails-protocol-fakes.patch"
    $patchedFile = Invoke-RepoGit -C $railsSource status --porcelain -- config/initializers/contentful_rails.rb
    if (-not $patchedFile) { Invoke-RepoGit -C $railsSource apply $railsPatch }
}

if ($Command -eq "up") {
    Invoke-Compose @("-f", $compose, "up", "-d", "--build")
    Wait-ParityEnvironment
} elseif ($Command -eq "down") {
    Invoke-Compose @("-f", $compose, "down")
} elseif ($Command -eq "reset") {
    Invoke-ResetDown
    Invoke-Compose @("-f", $compose, "up", "-d", "--build")
    Wait-ParityEnvironment
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
