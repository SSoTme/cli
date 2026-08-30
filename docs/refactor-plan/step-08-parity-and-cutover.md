# Step 08 — Parity check and squash-merge cutover

**Goal:** prove the rebuild is behaviorally identical to `legacy-final` except for the documented
`keep-modified` differences, then pivot `main` to the new tree in **one squash commit** and release.

## 1. `scripts/parity-check.sh`

1. Build the rebuild (`dotnet build Effortless.Cli.sln -c Release`).
2. Check out `legacy-final` into `../effortless-cli-legacy` (git worktree) and build it.
3. Run the E2E suite twice — `EFFORTLESS_CLI_MODE=legacy` against the legacy dll and `rebuild` against the
   new dll — with `EFFORTLESS_RECORD_GOLDENS=1` into two separate golden dirs.
4. Diff the two golden dirs. The only allowed differences are the ones enumerated in
   `docs/refactor-plan/parity-allowlist.txt` (generate it from the rulebook: every `keep-modified` row's
   test ids + the removed proxy retry lines). Exit non-zero on anything else.
5. Print a summary table: tests run, identical, allowed-different, unexpected.

Run it on macOS and Windows (the installers' platforms). Fix or document every unexpected difference.

## 2. Pre-merge checklist

- [ ] CI green on `effortless-cli` (all jobs).
- [ ] `parity-check.sh` clean on macOS + Windows.
- [ ] `npm install -g .` on a clean machine: `effortless -version`, `-help`, an `-init` + `install
      to-uppercase` + `build` + `clean` cycle against the live index.
- [ ] `installers/*` built via `workflow_dispatch` on a prerelease; installed on macOS (arm64) and Windows
      (x64); the four aliases work; `-upgradeCli` from the previous legacy version finds the new release.
- [ ] `docs/cli-reference.md` and `README.md` reviewed; the one-line legacy commit reference present.
- [ ] `docs/refactor-plan/README.md` decisions table updated with the final answers.
- [ ] Rulebook `RefactorSteps` all `done` except step-08; `TestCases` all `rebuild-green` or `Skip`-documented.

## 3. Cutover

1. Open the PR `effortless-cli` → `main` titled `Effortless CLI: clean REST-only rebuild` with the PR
   template filled; body starts with the legacy commit line and links the rulebook and this plan.
2. **Squash merge** (single commit). Do not delete the `effortless-cli` branch until the release is out.
3. On `main`: `scripts/release.sh` (stamps `package.json`, commits, pushes, creates `vYYYY-MM-DD.HH.MM`)
   → installers build → `update-airtable.yml` advertises the version.
4. `npm install -g .` locally; verify `effortless -version` prints the new stamp.
5. Post-release: `git tag rebuild-first-release <sha>`; update `README.md` if the legacy line needs the
   final wording; remove `scripts/test-legacy.sh` and the `legacy-parity` CI job (the safety net is now the
   rebuild's own suite); `RefactorSteps.step-08.Status` → `done`.

## 4. Rollback

`legacy/main` still builds and releases the old line; `git revert` of the squash commit restores the tree.
Because the version scheme is date-based there is no version conflict either way.
