$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$vectors = Join-Path $root 'TestResults/rails-note-encryption-vectors.json'
$dotnetVectors = Join-Path $root 'TestResults/rails-note-encryption-dotnet-vectors.json'

Push-Location $root
try {
    ruby tools/note-encryption-interop/rails_note_encryption_interop.rb generate --output $vectors
    ruby tools/note-encryption-interop/rails_note_encryption_interop.rb verify-rails --vectors $vectors

    $env:RAILS_NOTE_INTEROP_DOTNET_OUTPUT = $dotnetVectors
    try {
        dotnet test src/EarlyYearsFoundationRecovery.UnitTests/EarlyYearsFoundationRecovery.UnitTests.csproj `
            --no-restore `
            --filter 'FullyQualifiedName~RailsNoteEncryptionInteropTests'
    } finally {
        Remove-Item Env:RAILS_NOTE_INTEROP_DOTNET_OUTPUT -ErrorAction SilentlyContinue
    }

    ruby tools/note-encryption-interop/rails_note_encryption_interop.rb verify-dotnet `
        --input $dotnetVectors
} finally {
    Pop-Location
}
