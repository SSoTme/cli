# Step 07 — Resolve open decisions and harden

**Goal:** no `review-*` dispositions remain in use; every `blocked-by-decision` test is implemented or
deleted; the internal seams have unit coverage; the leftover static state is cleaned up.

**Owner decisions are resolved.** The rulebook now uses explicit `keep`,
`keep-modified`, and `drop-legacy` dispositions, and the historical
`review-drop`/`review-keep` rows no longer require confirmation.

## Per decision

| Decision | If DROP (default) | If KEEP |
|---|---|---|
| D1 seeds | Nothing to do (already excluded in Step 3). Delete `TestCases` rows for `listSeeds`/`cloneSeed` (none exist). | Port `ReplacementExtensions`, `RepositoryManager`, `DirectoryExtensions.StartSeedBuild/CheckSSoTmeCache`, `ListSeeds/CloneSeed/GetSeedUrl/InitiateCloneSeedingProcess`, `IsCurrentSeedRoot` into `src/Effortless.Cli.Core/Seeds/`; re-add `PluralizeService.Core`; hook `ApplySeedReplacementsAsync` into `ProjectLocator` and the reverse call into `CleanCommand`; add e2e tests with a local git seed fixture. |
| D2 buildOnTrigger | Nothing. | Port `ListenForChangesAndRebuild`/`CheckChanged` (without copilot) into `BuildCommand`; add a mock `/check` endpoint to `MockToolServer`; one e2e test with a short poll interval. |
| D3 auth bridge | **Resolved:** retain `login`, `projectLogin`, and `plan` as explicit non-networking stubs until the service endpoint is enabled; retain local `logout`; keep tools and `buildOnTrigger` unauthenticated. | Future work reimplements the magic-link service behind the retained command seam. |
| D4 pip | **Resolved:** removed. npm plus native MSI/PKG installers are the supported distribution paths. | — |
| D5 shim | Already done in Step 4. | (revert to join-and-ignore — not recommended) |
| D6 child spawn | Already done in Step 2. | (revert to PATH lookup — not recommended) |
| D7–D10 | Confirm rows; no code. | — |
| D11 parser forms | Normalize reserved commands so bareword, single-dash, and double-dash forms select the same option before a remaining argument can be treated as a transpiler. Apply this to `listTools` and `searchTools` too. | — |

The legacy Airtable metadata endpoint and its seed-replacement guessing are
also resolved as `drop-legacy`. Seed discovery, cloning, explicit `$key$`
replacement, and `buildOnTrigger` remain retained.

## Hardening

- **Done.** `BuildErrorLog` is now `sealed class BuildErrorLog` (instance, not static), held as
  `CliInvocation.BuildErrorLog` and threaded explicitly through `BuildRunner`'s constructor and the
  `RunCommandLine` delegate (`Func<string, EffortlessProject, bool, BuildErrorLog, int>`) so the
  per-step dispatcher call (`CommandDispatcher.Transpile`) records failures onto the *same* log the
  outer `BuildCommand` began and finishes — the legacy static field was implicitly process-wide shared
  state, so a naive per-invocation instance broke `-continueOnError`'s summary until this was threaded
  through explicitly. `CliLog.SuppressFileLog` became an `AsyncLocal<bool>`-backed property with zero
  call-site changes (its `[ThreadStatic]` field was already scoped per-thread; this makes it correctly
  scoped per async flow instead).
- **Moot.** `_hasRunRemoteToolsUpdate` and `_cloudBridgeRecoveryAttempted` do not exist anywhere in the
  ported code — Step 2 never created them (the closest real "has run X" latch,
  `CommandDispatcher._projectCatalogChecked`, was already a correctly-scoped instance field). Nothing to
  convert.
- **Partial.** `<Nullable>enable</Nullable>` on the whole `Effortless.Cli.Core.csproj` produces 766
  warnings across 48 of 59 files — not a "no logic changes" flip. Enabled via file-scoped `#nullable
  enable` pragma on the 5 files touched by this pass (`BuildErrorLog.cs`, `CliLog.cs`, `BuildRunner.cs`,
  `BuildCommand.cs`, `CliInvocation.cs`); the remaining ~54 files and ~700 warnings are follow-up work,
  one file (or small batch) at a time, same mechanism.
- **Done.** The 22 P2 test cases: `inst-dry-run-bareword-quirk` was deleted (conflicts with step-09 D27,
  which removes `-dryRun` entirely — implementing it would be throwaway). 3 live-network cases
  (`net-live-bridge-list`, `net-live-tool-run`, `net-upgrade-cli-check`) are `[Fact(Skip = "...")]` stubs
  in the new `tests/Effortless.Cli.E2E/Suites/NetworkTests.cs`, rulebook `Status: skipped-external`,
  mirroring the existing `res-dead-bridge-recovery` precedent. The other 18 are implemented and green.
- Delete `scripts/test-legacy.sh` **only** in Step 16.

**Done when:** `jq '.Dispositions.data[] | select(.NeedsUserConfirmation)'` returns nothing (or rows no
option/module/endpoint references), no `blocked-by-decision` statuses remain, CI green,
`RefactorSteps.step-07.Status` → `done`. **All satisfied as of 2026-09-04**, with the nullable rollout
explicitly partial (5/59 files) — tracked as follow-up, not silently dropped.
