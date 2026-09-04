# Step 10 — Rename `.ssotme` to `.effortless` with in-field migration; rebrand installers

**Goal:** `~/.effortless/` is the user state directory. Existing installs in the field (there are many)
are picked up automatically on first run and keep working. Installers and install paths say Effortless.

**Owner condition (2026-09-04):** the rename is approved *only* with a migration that picks up legacy
versions in the field. Copy, never move; a legacy `ssotme` binary that still reads `~/.ssotme` must keep
working alongside.

## User state directory

| Legacy | v2 |
|---|---|
| `~/.ssotme/` | `~/.effortless/` |
| `~/.ssotme/ssotme.key`, `ssotme.<runAs>.key` | `~/.effortless/effortless.key`, `effortless.<runAs>.key` |
| `~/.ssotme/remote_tools/ssotme-tools.json` | `~/.effortless/remote_tools/effortless-tools.json` |
| `~/.ssotme/remote_tools/ssotme.json` | dropped (v2 writes only `effortless.json` there) |
| everything else under `~/.ssotme/` | same relative path under `~/.effortless/` |
| env `SSOTME_CHILD_PROCESS` | `EFFORTLESS_CHILD_PROCESS` |

Migration rule (one `UserConfigMigration` module, the only place allowed to know the legacy names):

1. On every start, if `~/.effortless/` does not exist and `~/.ssotme/` does: copy the tree with the
   renames above, then write `~/.ssotme/MIGRATED-TO-EFFORTLESS` containing the UTC timestamp and the CLI
   version. Print one line: `Migrated ~/.ssotme to ~/.effortless (legacy directory left in place).`
2. If both exist: use `~/.effortless/` and never look at `~/.ssotme/` again. No merging.
3. Writes (`setAccountAPIKey`, tokens, tool URLs, catalog cache) go only to `~/.effortless/`.
4. Migration failure is fatal with a clear message; the CLI never runs half-migrated.

Nothing else in `src` may mention `ssotme` except the binary alias registration and this module.

## Project-level directory — owner decision needed

The per-project ledger dir `<root>/.ssotme/` (zfs ledgers, temp filesets) appears in every existing
project's `.gitignore` and in `init`'s template. Two options:

- **A (default in this plan):** rename to `<root>/.effortless/`. On project load, if `.effortless/` is
  absent and `.ssotme/` exists, rename the directory (it is gitignored build state, so a rename is safe).
  `init` writes `/**/.effortless/**/*` and keeps the `.ssotme` line in the template so old clones stay
  clean. `clean`/`purge` look in both until the rename has happened.
- **B:** leave `<root>/.ssotme/` alone. Zero churn in the field, one permanent `ssotme` in the tree.

Also rename `ssotme-seed.json` → `effortless-seed.json` (both accepted, v2 writes the new one).

## Installers

- macOS: `Effortless-Installer-<arch>.pkg`, payload under `/Applications/Effortless/`, `Resources/effortless`.
- Windows: `EffortlessInstaller.wixproj`, `Effortless-Installer_win-x64.msi`, `CreateEffortlessHomedir.ps1`
  creates `~/.effortless`.
- Both keep installing the four binary aliases.

## Tests

E2E, sandboxed home dir: `home-migrates-legacy-dir`, `home-migration-renames-key-files`,
`home-migration-leaves-legacy-untouched-and-marks-it`, `home-both-present-uses-effortless-only`,
`home-runas-key-mapping`, `home-migration-failure-is-fatal`, `project-ledger-dir-rename` (if A),
`child-env-var-renamed`. Update every existing e2e that seeds `~/.ssotme` to seed `~/.effortless` and add
one legacy-seeded variant per suite that proves the migration path.

## Done when

`grep -rn ssotme src` hits only the alias registration and the migration module; installers emit
Effortless-named artifacts; migration e2e green; `RefactorSteps.step-10.Status` → `done`.
