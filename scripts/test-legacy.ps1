$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LegacyRoot = Join-Path $RepoRoot ".git/legacy-test-$PID"
$LegacyDll = Join-Path $LegacyRoot "Windows/CLI/bin/Release/net8.0/SSoTme.OST.CLI.dll"

try {
    git -C $RepoRoot worktree add --detach $LegacyRoot legacy-final
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    dotnet build (Join-Path $LegacyRoot "SSoTme-OST-CLI.sln") -c Release
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $env:EFFORTLESS_CLI_UNDER_TEST = $LegacyDll
    $env:EFFORTLESS_CLI_MODE = "legacy"
    dotnet test (Join-Path $RepoRoot "tests/Effortless.Cli.E2E/Effortless.Cli.E2E.csproj") @args
    exit $LASTEXITCODE
}
finally {
    git -C $RepoRoot worktree remove --force $LegacyRoot
}
