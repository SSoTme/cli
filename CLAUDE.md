# Effortless CLI repository guide

This repository builds the REST-only `effortless` CLI. The official npm package is
`@effortlessapi/cli`, and all four binary names (`effortless`, `ssotme`,
`aicapture`, `aic`) point at `cli.js`. The unscoped npm package `effortless` is
unrelated software and must never be installed as this CLI.

## Source of truth

`effortless-rulebook/effortless-rulebook.json` is the single source of truth for
CLI options, dispatch behavior, wire contracts, messages, files, tests, and the
refactor plan. Query it with `jq`; do not read the whole file.

When changing generated options, bareword verbs, the CLI reference, or the test
manifest:

1. Edit the rulebook.
2. Run `npm run generate`.
3. Never hand-edit `*.g.cs` or other generated artifacts.

The only way to add or rename a CLI option is to change its rulebook row and
run `npm run generate`. CI rejects generated-file drift.

`npm run generate` writes six artifacts, all of them off-limits to hand edits:

- `src/Effortless.Cli.Core/Options/CliOptions.g.cs`
- `src/Effortless.Cli.Core/Options/BarewordVerbs.g.cs`
- `src/Effortless.Cli.Core/Options/CliOptionMetadata.g.cs` — tier, category,
  parent, family, help detail and example; what `-help <topic>` reads at runtime
- `docs/cli-reference.md`
- `tests/Effortless.Cli.E2E/TestManifest.g.json`, `tests/.../Tests/TestManifest.g.json`

`README.md` is the exception: only the text between its
`<!-- cli-commands:start -->` and `<!-- cli-commands:end -->` markers is
generated. Edit the rest of the README freely; never edit inside the markers.

Two traps worth knowing before you touch a `CliOptions` row:

- **`HelpSummary` has a 51-character budget**, not 72. `-help` renders
  `"  {Flag,-26} {HelpSummary}"`, so anything longer breaks the 80-column
  guarantee `meta-help-width` asserts. `help-summary-width` enforces it against
  the rulebook so it fails at authoring time rather than in an E2E run.
- **A bareword whose option takes a value needs a generator decision.** The
  `consumedStringOptions` set in `scripts/generate-from-rulebook.mjs` handles
  `verb <value>`; `parserHandledStringOptions` defers to
  `CliArgumentParser.NoDashCommandForms` for `verb <tool> <value>` shapes such as
  `pin`. A value-typed option left out of both is emitted as a `bool` setter and
  will not compile.

## Repository layout

- `src/Effortless.Cli/` — .NET 8 executable.
- `src/Effortless.Cli.Core/` — parser, dispatcher, project, REST, auth, file-set,
  configuration, seeds, cloud-trigger watching, and update logic.
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

The supported public install is `npm install -g @effortlessapi/cli`.

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

## Retained seed and trigger features

- D1 keeps `listSeeds`, `cloneSeed`, and the `ssotme-seed.json` `$key$`
  replacement hook. A seed is discovered only when its public repository has
  `effortless.json` at the root. Cloning preserves `.git` and does not
  automatically execute downloaded code.
- D2 keeps `build -buildOnTrigger <baseId>` as a lightweight cloud-trigger
  monitor independent of the rulebook editor. Polling failures are fatal; never
  hide them as an unchanged trigger.
- Airtable schema guessing for seed replacements is removed. Do not remove seed
  discovery, cloning, explicit `ssotme-seed.json` `$key$` replacements, or
  `buildOnTrigger`.

## Authentication status

`login`, `projectLogin`, and `plan` are intentionally non-networking stubs until
the server-side magic-link endpoint is enabled. They must fail clearly and must
not write tokens or call the bridge. `logout` still clears local tokens. Normal
tool execution and `buildOnTrigger` do not require authentication.

## Project save and upgrade invariants

- `EffortlessProject.Save()` merges unknown custom transpiler properties from the
  on-disk `effortless.json`, but must never restore model-owned properties such as
  `PinnedVersion` or `LastVersionUsed`. `PinnedVersion` uses
  `IgnoreAndPopulate`; copying the old value back would silently undo an unpin.
- With no hard pin, resolution uses catalog HEAD. `LastVersionUsed` is
  informational and records what ran; it does not select a version. Upgrade paths
  clear `PinnedVersion`, remove embedded command-line versions, and advance
  `LastVersionUsed` to HEAD.
- **A pin the catalog can still satisfy survives a build (D17).**
  `ProjectToolUpgradePlan.Apply` takes `clearPins`: the automatic currentness
  gate in `CommandDispatcher.EnsureProjectToolsCurrent` passes `false`, so
  `-pin` actually takes effect; only `-upgrade` / `-upgradeAll` pass `true` and
  unpin. A pin that no longer resolves is stale rather than deliberate and the
  gate still clears it, which is what keeps step-03A's "a stale pin must never
  silently break a build" guarantee intact. Do not collapse these two cases back
  into one.

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
