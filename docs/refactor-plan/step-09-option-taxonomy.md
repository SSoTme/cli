# Step 09 — Option taxonomy, tiers, rationale, and filterable help

**Goal:** the 54 kept options stop being one flat list. Every option carries an owner-authored rationale,
a tier, a parent command when it is a modifier, and clean help text. `-help` is grouped and filterable.
All of it lives in the rulebook and is generated from there.

**Input:** the owner's answers to the option question list from the 2026-09-04 session. Nothing in this
step is guessed; each answer becomes a rulebook row edit first, code second.

## Why

The port kept 54 of 67 options on purpose (parity first). The owner confirmed that few of them can go,
but that they need to be explained better and made filterable. The codebase dates to 2017 and the
vocabulary to 2005; the rulebook is where that reasoning finally gets written down next to the option.

## Rulebook changes

New `CliOptions` fields (all generated artifacts read them):

| Field | Type | Meaning |
|---|---|---|
| `Tier` | enum `primary` / `secondary` / `modifier` / `plumbing` | primary = the verbs a new user needs on day one; secondary = real but occasional; modifier = only meaningful with a parent command; plumbing = exists for scripts/CI/tooling |
| `ParentOption` | relationship → `CliOptions` | for modifiers: the command they modify (`purge`→`clean`, `includeDisabled`→`build`, `projectName`→`init`, `transpilerGroup`→`install`, `dryRun`→`install`, `latest`→transpile, ...) |
| `Rationale` | string | the owner's "why this exists", verbatim-ish from the Q&A |
| `HelpSummary` | string | one line, ≤ 72 chars, shown in `-help` |
| `HelpDetail` | string | the longer text shown by `-help <option>` and in `cli-reference.md` |
| `Example` | string | one realistic invocation |

`OptionCategories` gains `ParentCategory` (nullable) so `build` / `clean` / `install` can sit under a
`project-pipeline` group and `tool-resolution` / `tool-urls` under `tools`, and `Tier` for the category's
default. Recategorize `buildOnTrigger` (`legacy-other` → `build`) and rewrite the `seeds` description
(it still says "recommended drop"; the owner kept it in D1).

Naming clean-ups decided by the Q&A are applied as `keep-modified` rows with the old form kept as an
alias: `setUrl`/`setToolUrl` family (D9), `authenticate` vs `login`, `subscription` vs `plan`.

Help-text fixes are `keep-modified`: typos (`bing`, `buid`), "SSoT.me" → "Effortless", and the `dryRun`
bareword quirk (D11 makes bareword/`-x`/`--x` equivalent for every reserved word).

## Generator and code

- `CliOptions.g.cs` emits tier, category, parent and summary metadata alongside the Plossum attributes.
- `docs/cli-reference.md` regenerates grouped by category → tier, modifiers nested under their parent.
- `-help` (no argument) prints primary-tier commands grouped by category, one line each, and ends with
  `effortless -help <category|option>` and `effortless -help all`.
- `-help <category>` prints that category, all tiers. `-help <option>` prints `HelpDetail`, aliases,
  bareword forms, parent, and the example.
- `-help all` is the legacy flat dump.

## Tests

- Existing `help-*` goldens become `keep-modified` and are re-recorded.
- New: `help-default-is-primary-only`, `help-category-filter`, `help-option-detail`,
  `help-unknown-topic`, `help-all-flat`, `help-summary-width` (≤ 72), and one rulebook validation rule:
  every `keep*` option has `Tier`, `Rationale`, `HelpSummary`; every `modifier` has `ParentOption`.

## Done when

No kept option lacks `Tier` / `Rationale` / `HelpSummary`; modifiers name their parent; `-help` fits one
screen; `-help <topic>` works; typos and quirks fixed; `cli-reference.md` regenerated; CI drift check
passes; `RefactorSteps.step-09.Status` → `done`.
