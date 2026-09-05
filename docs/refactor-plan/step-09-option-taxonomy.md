# Step 09 — Option taxonomy, verb families, and the v2 option decisions

**Goal:** the 54 kept options stop being one flat list. Every option carries an owner-authored rationale,
a tier, a parent command when it is a modifier, and clean help text. Verbs that scope over the project
follow one consistent family pattern. The owner's per-option decisions from 2026-09-04 (README D12–D27)
are encoded in the rulebook first and implemented second. `-help` and the README are generated from it.

## Why

The port kept 54 of 67 options on purpose (parity first). The owner confirmed that few can be removed,
most need updating, and they need tiers and a hierarchy. The codebase dates to 2017 and the vocabulary to
2005; the rulebook is where that reasoning gets written down next to each option.

## 1. Rulebook schema changes

New `CliOptions` fields (every generated artifact reads them):

| Field | Type | Meaning |
|---|---|---|
| `Tier` | enum `primary` / `secondary` / `modifier` / `plumbing` | primary = day-one verbs; secondary = real but occasional; modifier = only meaningful with a parent command; plumbing = for scripts, CI, tooling |
| `ParentOption` | relationship → `CliOptions` | for modifiers: the command they modify |
| `VerbFamily` | nullable string | `build`, `clean`, `describe` (see §2) |
| `Scope` | nullable enum `downstream` / `local` / `all` / `withSubprojects` | the family member's scope |
| `Rationale` | string | the owner's "why this exists", from the Q&A |
| `HelpSummary` | string | one line shown in `-help`, ≤ **51** chars (see the correction below) |
| `HelpDetail` | string | shown by `-help <option>` and in `cli-reference.md` |
| `Example` | string | one realistic invocation |

`OptionCategories` gains `ParentCategory` (nullable) and `Tier`. New grouping: `project-pipeline`
(build, clean, install, describe families), `tools` (tool-resolution, tool-urls, local tools), `account`
(auth, keys), `seeds`, `cli-meta`. Recategorize `buildOnTrigger` (`legacy-other` → `build`). Rewrite the
`seeds` description (it still says "recommended drop"; D1 kept it).

A rulebook validation rule: every `keep*` option has `Tier`, `Rationale`, `HelpSummary`; every
`modifier` has `ParentOption`; every `VerbFamily` has exactly the four scopes.

## 2. Verb families (D12)

One pattern for every verb that scopes over the project tree:

| Scope | Suffix | Meaning |
|---|---|---|
| `downstream` | *(none)* | this folder and its child folders, within this project |
| `local` | `Local` | exactly this folder |
| `all` | `All` | as if run from the project root (whole project) |
| `withSubprojects` | `WithSubprojects` | `All`, plus nested effortless projects, which are normally excluded |

Applied to `build`, `clean`, and `describe`. New rows: `buildWithSubprojects`, `cleanWithSubprojects`,
`describeLocal`, `describeWithSubprojects`. Behavior change (`keep-modified`): `describe` today means the
local folder only; in v2 it means downstream, like `build`, and `describeLocal` is the old behavior.
Bareword forms are lowercased so `buildwithsubprojects` also matches.

## 3. Per-option decisions to encode

| Option(s) | Decision | Row change |
|---|---|---|
| `transpilerGroup` (D14) | Keep. It is the way to say "only run the postgres steps". | Tier secondary; Rationale; Example `effortless build -transpilerGroup postgres` |
| `targetUrl` (D15) | Keep as a real option. A name in the tool position plus `-targetUrl` is a temporary override that does not lose what the tool is; a bare URL in the tool position stays supported. | Tier modifier (parent: the transpile invocation); help rewritten; typo `bing` fixed |
| `setUrl` / `removeUrl` / `viewUrl` / `listUrls` (D16) | `ToolUrl` is canonical: `setToolUrl`, `removeToolUrl`, `viewToolUrl`, `listToolUrls`. The `*Url` forms were a mistake; keep them as hidden aliases. | Rename ids; old flags into `Aliases`; `keep-modified` |
| `latest`, `upgrade`, `upgradeAll`, new `pin` (D17) | A tool is pinned or it follows HEAD. New `pin <version\|url>` pins **this project's** step to a catalog version or a specific URL (stored in `effortless.json`; distinct from the per-user `setToolUrl` override). `upgrade` removes the pin (alias `unpin`, already a bareword); `upgradeAll` removes all pins. `latest` is removed: it is an attribute, not a command. Default stays unpinned = HEAD. Pin-on-install by default is **not** assumed; the owner can flip that. | `latest` → `drop-legacy`; add `pin`; `upgrade` help rewritten; tests `res-latest-flag` deleted, `res-head-latest` reviewed |
| `authenticate` / `projectLogin` / `logout` / `subscription` (D18) | Shown in help. Displayed names `login`, `projectLogin`, `logout`, `plan`; `authenticate` and `subscription` remain aliases (owner did not pick; flip if wanted). Wiring to the real workload is step 13. | Ids renamed to `login`/`plan`; Tier secondary |
| `setAccountAPIKey`, `account` (D19) | Keep. A stored account key is injected automatically as a parameter when a tool in that account runs (`-setAccountKey foo=bar` then `effortless foo/whatever -p x=5` also sends `foo=bar`), and `-account foo` selects it explicitly. Where the key is stored is fungible (step 10 moves it to `~/.effortless/effortless.key`). No special case is removed. | Add alias `setAccountKey`; Tier plumbing / modifier; Rationale |
| `acct/tool` extraction (D20) | Never fail on it. If `acct/tool` does not resolve in the catalog, resolve `tool` and behave as `tool -account acct`. Fail only if `tool` does not resolve either. | `ToolResolutionRules` R6/R7 rewritten; `keep-modified` |
| `output` (D21) | It is whatever the tool decides. The CLI passes it to the tool as `OutputFileName`; the tool may write one file or a tree. | Help rewritten; step 12 passes it as `EFFORTLESS_OUTPUT_NAME` |
| `includeDisabled`, new `enable` / `disable` (D22) | Add `disable` and `enable` verbs that set `IsDisabled` on a step, targeted the same way `uninstall` targets a step (tool name in the current folder, optional `transpilerGroup`). | Add rows; Tier secondary; `includeDisabled` becomes modifier of `build` |
| `info` / `listSettings` / `describe` (D23) | Confirmed: `info` is user-level state, `listSettings` is project settings, `describe` is steps. `info` also prints the catalog's last-refresh time and age (step 11 implements). | Rationale; help rewritten |
| `buildOnTrigger` (D24) | Keep, category `build`, tier secondary. It polls a tiny bridge workload with no auth, DB, or storage. **Verified route** (`TriggerBuildWatcher`): the CLI issues `GET {bridge}/check?baseId={id}` every 3 s and reads JSON `{changed: bool}`, rebuilding after a 10 s quiet period; polling, HTTP and payload failures are fatal rather than treated as unchanged. There is no `/set` or `/get` route in the codebase — earlier drafts of this row were wrong; how the Airtable side flips `changed` is outside this repo. | Recategorize; Rationale; endpoint row filled |
| `purge` / `preserveZFS` / `skipClean` | Keep, tiers modifier (`clean`) / modifier (`clean`) / modifier (transpile). Help rewritten to say what they do (ledger kept vs deleted; previous output not cleaned before writing). Redefinition of the ledger itself is step 15. | Help; Rationale |
| `execute` (D26) | Keep. A shell step with no fileset; local tools (step 12) coexist with it. | Tier secondary; Rationale |
| `dryRun` (D27) | **Removed.** It made no sense: early steps write files that later steps read, so a "run but do not write" build is not a build. Git on a clean tree is the dry run. | `drop-legacy`; tests `inst-dry-run` deleted, `inst-dry-run-bareword-quirk` deleted |
| `describe` family, `build` family, `clean` family (D12) | See §2. | New rows; `describe` `keep-modified` |
| `listSeeds` / `cloneSeed` (D25) | Kept; seed sources and the skill are step 14. | Tier secondary here |
| `runAs` (D7) | Keep. | Tier plumbing |
| `help` | Grouped and filterable (§4). | `keep-modified` |

Naming and text fixes, all `keep-modified` with the old form kept as an alias where a name changes:
typos (`bing`, `buid`), "SSoT.me" → "Effortless" in every help string, and D11 (bareword, `-x`, and `--x`
are equivalent for every reserved word, so the old `dryRun`-style bareword quirk cannot recur).

## 4. Generator and code

- `CliOptions.g.cs` emits tier, category, parent, family/scope and summary metadata alongside the
  Plossum attributes; the dispatcher reads family/scope instead of per-verb `if` chains.
- `docs/cli-reference.md` regenerates grouped by category → tier, modifiers nested under their parent,
  each family shown as one table of four scopes.
- `README.md` gets a generated command summary between `<!-- cli-commands:start/end -->` markers so the
  README and `-help` cannot drift (owner: "make sure all of these new and changed terms are well
  documented in the readme and -help screens").
- `-help` prints primary-tier commands grouped by category, one line each, then
  `effortless -help <category|option>` and `effortless -help all`. `-help <category>` prints that
  category, all tiers. `-help <option>` prints `HelpDetail`, aliases, bareword forms, parent, example.
  `-help all` is the flat dump.

## 5. Tests

- Existing `help-*` goldens become `keep-modified` and are re-recorded.
- New: `help-default-is-primary-only`, `help-category-filter`, `help-option-detail`, `help-unknown-topic`,
  `help-all-flat`, `help-summary-width`, `readme-commands-block-matches-rulebook`.
- Families: `build-with-subprojects-includes-nested`, `build-all-excludes-nested`, `clean-with-subprojects`,
  `describe-downstream`, `describe-local`, `describe-all`, `describe-with-subprojects`.
- Pinning: `pin-version`, `pin-url`, `pin-then-build-uses-pin`, `upgrade-removes-pin`,
  `upgrade-all-removes-pins`, `latest-flag-rejected`.
- `enable-step`, `disable-step`, `build-skips-disabled`, `build-include-disabled`.
- `acct-tool-falls-back-to-account`, `acct-tool-unresolved-tool-fails`.
- `tool-url-verbs-canonical`, `tool-url-verbs-legacy-aliases`.
- `dry-run-rejected`.

## Corrections found while implementing (2026-09-05)

Three points where this document's prose did not match the verified code. The
rulebook carries the corrected version in every case; trust it over the text
above.

- **`HelpSummary`'s real budget is 51 chars, not 72.** `-help` renders
  `"  {Flag,-26} {HelpSummary}"`, so 72 would break the 80-column guarantee
  `meta-help-width` asserts. `help-summary-width` enforces 51 against the
  rulebook so an over-long summary fails at authoring time.
- **`buildAll` / `cleanAll` already walked into nested projects.** §2 presents
  `WithSubprojects` as purely additive, but v1's `buildAll`/`cleanAll` already
  spawned a child CLI in every nested `effortless.json`. Implementing D6/D12 as
  written is therefore a *behavior change to `All`*, not just four new rows:
  `All` now stops at nested project boundaries and the traversal moved to the
  new `WithSubprojects` scope. `build-all` and `clean-all` were re-recorded, and
  `build-all-excludes-nested` guards the new boundary.
- **`describe` was already downstream.** §2 says v1's `describe` meant the local
  folder only. It did not: `EffortlessProject.Describe(cwd)` called
  `IsAtPath(relativePath)` with `exactMatch: false`, which is downstream, and
  `proj-describe-subtree` had been asserting that all along. No runtime change
  was needed for `describe`; only `describeLocal` (the genuinely new
  exact-folder filter) had to be built.
- **`-latest`'s removal collides with step-03A's currentness gate.** The gate
  cleared `PinnedVersion` on *every* build, which would have made the new `-pin`
  a no-op. Reconciled by splitting the two intents: a pin the catalog can still
  resolve is deliberate and is honored by the gate, while a pin that no longer
  resolves is stale and is still cleared. `-upgrade`/`-upgradeAll` remain the
  only paths that unpin unconditionally. `build-pinned-url`,
  `build-version-label`, `build-sync-commandline-version` and
  `res-freshness-stale-upgrades` were re-recorded for this; `build-pinned-missing`
  and `res-freshness-upgrade-atomic` were unaffected.

## Done when

No kept option lacks `Tier` / `Rationale` / `HelpSummary`; modifiers name their parent; the three verb
families have all four scopes; `pin`, `enable`, `disable`, `*ToolUrl` exist; `latest` and `dryRun` are
gone; `-help` fits one screen; `-help <topic>` works; README block generated; typos and quirks fixed;
CI drift check passes; `dotnet test` green; `RefactorSteps.step-09.Status` → `done`.
