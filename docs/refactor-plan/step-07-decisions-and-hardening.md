# Step 07 — Resolve open decisions and harden

**Goal:** no `review-*` dispositions remain in use; every `blocked-by-decision` test is implemented or
deleted; the internal seams have unit coverage; the leftover static state is cleaned up.

**Blocked until the owner answers D1–D11 in [README.md#decisions](README.md#decisions).** Record each
answer by editing the rulebook: change the `Disposition` on the affected rows to `keep`/`keep-modified`/
`drop-legacy`, and set `Dispositions.review-drop/review-keep.NeedsUserConfirmation` to `false` once no row
uses them (or delete the rows).

## Per decision

| Decision | If DROP (default) | If KEEP |
|---|---|---|
| D1 seeds | Nothing to do (already excluded in Step 3). Delete `TestCases` rows for `listSeeds`/`cloneSeed` (none exist). | Port `ReplacementExtensions`, `RepositoryManager`, `DirectoryExtensions.StartSeedBuild/CheckSSoTmeCache`, `ListSeeds/CloneSeed/GetSeedUrl/InitiateCloneSeedingProcess`, `IsCurrentSeedRoot` into `src/Effortless.Cli.Core/Seeds/`; re-add `PluralizeService.Core`; hook `ApplySeedReplacementsAsync` into `ProjectLocator` and the reverse call into `CleanCommand`; add e2e tests with a local git seed fixture. |
| D2 buildOnTrigger | Nothing. | Port `ListenForChangesAndRebuild`/`CheckChanged` (without copilot) into `BuildCommand`; add a mock `/check` endpoint to `MockToolServer`; one e2e test with a short poll interval. |
| D3 auth bridge | Keep `CloudBridgeClient.AuthCallsEnabled = false`; tests `auth-login-flow`, `auth-project-login`, `auth-subscription` stay `blocked-by-decision` → change to `Skip` with the reason. | Set `AuthCallsEnabled = true` **only** for `login`, `projectLogin`, `subscription`, `info` plan lookup and token refresh; keep the temp-dir/minimal-project mechanism (or call the bridge URL directly with the same payload — the wire contract is the same); restore `CheckLoginStatus`-style refresh in `info`; implement the three tests against the mock bridge; never call the bridge from build/transpile paths (add a unit test that asserts `TranspileClient` has no reference to `CloudBridgeClient`). |
| D4 pip | Nothing (already deleted in Step 4). | Restore `setup.py`, `ssotme/cli.py`, `setup.yml` with new paths. |
| D5 shim | Already done in Step 4. | (revert to join-and-ignore — not recommended) |
| D6 child spawn | Already done in Step 2. | (revert to PATH lookup — not recommended) |
| D7–D10 | Confirm rows; no code. | — |
| D11 parser forms | Normalize reserved commands so bareword, single-dash, and double-dash forms select the same option before a remaining argument can be treated as a transpiler. Apply this to `listTools` and `searchTools` too. | — |

## Hardening

- Replace the static mutable state left in Step 2 (`BuildErrorLog`, `_hasRunRemoteToolsUpdate`,
  `_cloudBridgeRecoveryAttempted`, `CliLog.SuppressFileLog`) with per-invocation instances passed through
  `CliInvocation`; keep behavior identical (the E2E suite proves it).
- Enable `<Nullable>enable</Nullable>` file by file in Core; no logic changes.
- Implement the remaining `unit-core` P1 rows and any P2 e2e rows still `planned` (or mark them `Skip`
  with a reason in the manifest).
- Delete `scripts/test-legacy.sh` **only** in Step 8.

**Done when:** `jq '.Dispositions.data[] | select(.NeedsUserConfirmation)'` returns nothing (or rows no
option/module/endpoint references), no `blocked-by-decision` statuses remain, CI green,
`RefactorSteps.step-07.Status` → `done`.
