$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LegacyDll = Join-Path $RepoRoot "Windows/CLI/bin/Release/net8.0/SSoTme.OST.CLI.dll"

dotnet build (Join-Path $RepoRoot "SSoTme-OST-CLI.sln") -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$env:EFFORTLESS_CLI_UNDER_TEST = $LegacyDll
$env:EFFORTLESS_CLI_MODE = "legacy"
dotnet test (Join-Path $RepoRoot "tests/Effortless.Cli.E2E/Effortless.Cli.E2E.csproj") @args
exit $LASTEXITCODE
