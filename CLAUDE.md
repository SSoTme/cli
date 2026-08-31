# Effortless CLI repository guide

This repository builds the REST-only `effortless` CLI. The npm package remains
`ssotme`, and all four binary names (`effortless`, `ssotme`, `aicapture`, `aic`)
point at `cli.js`.

## Source of truth

`effortless-rulebook/effortless-rulebook.json` is the single source of truth for
CLI options, dispatch behavior, wire contracts, messages, files, tests, and the
refactor plan. Query it with `jq`; do not read the whole file.

When changing generated options, bareword verbs, the CLI reference, or the test
manifest:

1. Edit the rulebook.
2. Run `npm run generate`.
3. Never hand-edit `*.g.cs` or other generated artifacts.

The generated pipeline is introduced in Step 5. Until then, keep the checked-in
generated files synchronized with the rulebook changes in the same commit.

## Repository layout

- `src/Effortless.Cli/` — .NET 8 executable.
- `src/Effortless.Cli.Core/` — parser, dispatcher, project, REST, auth, file-set,
  configuration, and update logic.
- `tests/Effortless.Cli.Tests/` — unit and wire-contract tests.
- `tests/Effortless.Cli.E2E/` — black-box CLI tests and mock HTTP servers.
- `tests/fixtures/` — project, catalog, wire, shim, and golden fixtures.
- `installers/windows/` and `installers/macos/` — MSI and PKG sources.
- `scripts/` — generation, parity, legacy-test, CI, and release scripts.
- `docs/refactor-plan/` — the staged rebuild plan and historical record.

## Build and test

```bash
dotnet build Effortless.Cli.sln --configuration Release
dotnet test Effortless.Cli.sln --configuration Release
```

Run `dotnet test` before every commit. The E2E harness uses the rebuilt DLL by
default after the legacy tree is removed.

For npm-shim testing:

```bash
npm install -g .
effortless -version
ssotme -v
aicapture -v
aic -v
```

## Local tool URL debugging

Point a catalog tool at a local HTTP service, run the command or build, then
remove the override:

```bash
effortless -setToolUrl tool-name=http://localhost:PORT
effortless tool-name -debug
effortless -removeToolUrl tool-name
```

Do not add a stale-catalog, alternate-transport, or RabbitMQ fallback. A catalog
refresh failure is a hard failure.

## Project save and upgrade invariants

- `EffortlessProject.Save()` merges unknown custom transpiler properties from the
  on-disk `effortless.json`, but must never restore model-owned properties such as
  `PinnedVersion` or `LastVersionUsed`. `PinnedVersion` uses
  `IgnoreAndPopulate`; copying the old value back would silently undo an unpin.
- With no hard pin, resolution uses catalog HEAD. `LastVersionUsed` is
  informational and records what ran; it does not select a version. Upgrade paths
  clear `PinnedVersion`, remove embedded command-line versions, and advance
  `LastVersionUsed` to HEAD.

## Releases

Commit source changes first, then use the guarded release flow:

```bash
scripts/release.sh
```

The release script owns version stamping, pushing, and release creation. Do not
hand-edit release versions or invent a publish procedure.

## Branch safety

The final RabbitMQ-era source is commit
`a8f0f320f4417c58fa931cc4bc8164f79cbbbd97` (`legacy-final`,
`legacy/main`). Do not modify `main` or `legacy/main` while completing the
REST-only rebuild branch.
