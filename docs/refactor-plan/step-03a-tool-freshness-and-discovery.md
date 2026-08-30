# Step 03A — Automatic tool freshness and catalog discovery

**Goal:** no catalog-dependent command uses a remote tools index that the CLI has not successfully
confirmed within the preceding 24 hours; before project tool execution, every remotely versioned project
step is brought to the current catalog HEAD; users can list and search the same catalog.

This is intentionally a separate new-behavior step after the legacy port and before the old tree is cut.
It is implemented in the existing resolver/version-command code and tested through the existing unit,
E2E, mock-bridge, fixture, manifest, and Linux/macOS/Windows CI paths.

Inputs: `CliOptions.listTools/searchTools`, `ToolResolutionRules.R0-catalog-freshness`,
`LifecycleStates.S08a-project-tools-current`, its `StateTransitions`, `ConfigFiles.remote-tools-index`,
the new `UserMessages`, and every `TestCases` row whose `Status` is `planned-step-03a`.

## 1. Freshness contract

- `RemoteToolsIndex` owns `fetchedAt`. Stamp it with the injected `TimeProvider` only after a bridge
  response parses, contains a structurally valid tool map, and has been installed atomically.
- Before any catalog read or remote project-tool execution, treat the cache as stale when `fetchedAt` is
  missing, malformed, more than five minutes in the future, or **age >= 24 hours**.
- A stale cache causes one synchronous bridge refresh before the requested action. The CLI cannot update
  itself while it is not running; this gate guarantees freshness whenever catalog-dependent work runs.
- A failed refresh is a hard failure. Return non-zero, do not resolve or execute from the stale cache, do
  not advance `fetchedAt`, and do not change `effortless.json`. Preserve the old cache only as diagnostic
  evidence. There is no RabbitMQ or stale-cache fallback.
- `help`, `version`, auth, tool-URL management, settings, describe, and clean remain offline. Explicit URL
  execution and `-execute` steps are not remote-catalog tools and are excluded.

Implement this as a small `CatalogFreshnessPolicy`/`EnsureFreshAsync` seam using .NET 8 `TimeProvider`.
Tests inject a fixed clock; production uses `TimeProvider.System`. Never test the 24-hour boundary by
sleeping or by relying on wall-clock timing.

## 2. Automatic project upgrade

After freshness is established, use the loaded project or lazily locate one from the current directory
for management commands. Inspect every remotely versioned, non-`execute` step before running the
requested tool/build/list/search/refresh:

1. Resolve the catalog HEAD for **every** eligible step into an in-memory change set.
2. A hard pin, a missing `LastVersionUsed`, or `LastVersionUsed != HEAD` means the step needs the same
   state transition as `-upgrade`: clear `PinnedVersion` and set `LastVersionUsed` to HEAD.
3. If any step is absent from the catalog or has no HEAD, fail the invocation and discard the entire
   change set. Name the failing step; do not run the requested command.
4. If all steps resolve, apply the changes and call `ProjectFileStore.Save` once. A project already at
   HEAD produces no file write.

This strict all-or-nothing path is for automatic freshness enforcement. The explicit legacy-compatible
`-upgradeAll` command keeps its documented `SKIP` behavior. Catalog list/search still work outside a
project; in that case there is no project to upgrade, so only catalog freshness is enforced.
Extract one shared HEAD-resolution/change-planning routine for both paths; pass an explicit policy that
chooses strict failure or skip behavior rather than duplicating upgrade logic.

## 3. List and search commands

Add these generated options and route both through `RemoteToolsIndex`:

```text
effortless listTools
effortless searchTools <query>
```

`-listTools`/`-lt` and `-searchTools`/`-st` are equivalent flag forms. D11's global argument
normalization later supplies the double-dash forms consistently with every other command.

- Collapse the version map to one row per canonical `<account>/<package>/<tool>` name.
- Sort canonical names with ordinal, case-insensitive comparison.
- Print the current HEAD version. Keep tools without a HEAD visible as `NO HEAD`; never drop them.
- Search is a case-insensitive ordinal substring match over canonical and short names.
- The index contract does not guarantee descriptions, so do not synthesize or display one.
- A valid empty search prints `No tools matched '<query>'.` and exits 0.
- Update the generic tool-not-found hint to point at `effortless listTools`; `listToolUrls` lists custom
  overrides, not the remote catalog.

## 4. Tests and fixtures

Implement all `planned-step-03a` rows without creating a parallel harness:

- list projection/order/no-HEAD visibility; canonical and short-name search; explicit no-match output;
- fresh at 23:59:59 versus stale at exactly 24:00:00; missing/malformed/future timestamps;
- stale refresh followed by automatic unpin/HEAD advancement before a build;
- refresh failure: non-zero, no requested POST, no stale-cache use, no project mutation;
- automatic upgrade transaction failure: no partial project mutation and no build;
- help/version remain offline;
- unit coverage of the `TimeProvider` freshness policy.

The mock bridge must be able to return a valid replacement index, malformed JSON, an invalid/empty tool
map, and transport failure. Compare project and cache bytes before/after failure cases. Run the same
`Effortless.Cli.E2E` and `Effortless.Cli.Tests` commands from Step 3 on all three CI operating systems.

When green, change every `planned-step-03a` test to `rebuild-green` and
`RefactorSteps.step-03a.Status` to `done`. Step 4 must not begin before this step is done.
