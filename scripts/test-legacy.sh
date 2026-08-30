#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
legacy_dll="$repo_root/Windows/CLI/bin/Release/net8.0/SSoTme.OST.CLI.dll"

dotnet build "$repo_root/SSoTme-OST-CLI.sln" -c Release

EFFORTLESS_CLI_UNDER_TEST="$legacy_dll" \
EFFORTLESS_CLI_MODE=legacy \
dotnet test "$repo_root/tests/Effortless.Cli.E2E/Effortless.Cli.E2E.csproj" "$@"
