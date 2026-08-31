#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
legacy_root="$repo_root/.git/legacy-test-$$"
legacy_dll="$legacy_root/Windows/CLI/bin/Release/net8.0/SSoTme.OST.CLI.dll"

cleanup() {
    git -C "$repo_root" worktree remove --force "$legacy_root"
}
trap cleanup EXIT

git -C "$repo_root" worktree add --detach "$legacy_root" legacy-final
dotnet build "$legacy_root/SSoTme-OST-CLI.sln" -c Release

EFFORTLESS_CLI_UNDER_TEST="$legacy_dll" \
EFFORTLESS_CLI_MODE=legacy \
dotnet test "$repo_root/tests/Effortless.Cli.E2E/Effortless.Cli.E2E.csproj" "$@"
