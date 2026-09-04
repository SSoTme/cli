# Step 13 — Parity check, v2 clean cut, and single-commit cutover

**Goal:** prove the rebuild is behaviorally identical to `legacy-final` except for the documented
`keep-modified` differences, strip every trace of v1 that is not the migration story out of the shipping
tree, then land v2 on `main` as **one squash commit** with **one push**.

This step was `step-08`. It moved to the end so the v2 feature steps (09–12) ship inside the same single
commit. Until this step, **nothing is pushed**; the branch is polished locally.

## 1. `scripts/parity-check.sh`

1. Build the rebuild (`dotnet build Effortless.Cli.sln -c Release`).
2. Check out `legacy-final` into `../effortless-cli-legacy` (git worktree) and build it.
3. Run the E2E suite twice — `EFFORTLESS_CLI_MODE=legacy` against the legacy dll and `rebuild` against the
   new dll — with `EFFORTLESS_RECORD_GOLDENS=1` into two separate golden dirs.
4. Diff the two golden dirs. The only allowed differences are the ones enumerated in
   `docs/refactor-plan/parity-allowlist.txt` (generated from the rulebook: every `keep-modified` row's test
   ids, the removed proxy retry lines, and the step-09/10 renames). Exit non-zero on anything else.
5. Print a summary table: tests run, identical, allowed-different, unexpected.

Run it on macOS and Windows (the installers' platforms). Fix or document every unexpected difference.

## 2. The v2 clean cut

Owner decision (2026-09-04): v2 must not carry v1's explanations. A reader of the shipping tree should
learn how v2 works, not how v1 used to. The migration story lives in exactly one page.

### 2a. Rulebook split

- Move every `drop-*` row (`CliOptions`, `SourceModules`, `HttpEndpoints`, `EntryPoints`, `DevopsPipelines`),
  the `legacy-*` and `dead` `OptionCategories`, the `drop-*` `Dispositions`, and every `Description`/`Notes`
  sentence that explains RabbitMQ-era behavior into `effortless-rulebook/archive/v1-migration-rulebook.json`.
  The shipping `effortless-rulebook.json` describes v2 only.
- `RefactorSteps` and `docs/refactor-plan/` move with the archive (they are history once v2 ships). Keep a
  one-paragraph pointer in `README.md`.
- `ToolResolutionRules` R7/R10 lose their "the RabbitMQ fallback is removed" wording; state the v2 rule only.
- `ConfigFiles` rows lose "RabbitMQ-era" annotations; `ssotme.key` fields that are unused are removed from the
  v2 key-file format (step-10 already renames the file).

### 2b. Generated docs

- Remove the **Removed in the rebuild** appendix from `docs/cli-reference.md` (generator change).
- Add `docs/migrating-from-ssotme-v1.md`: one page. Allowed to say RabbitMQ once. Covers: package/binary
  names, `~/.ssotme` → `~/.effortless` (automatic), `ssotme.json` → `effortless.json` (automatic), removed
  options and their replacements, the legacy commit reference.

### 2c. Tests and scripts

- Delete `wire-legacy-request-compat` and `tests/fixtures/wire/legacy-request.json`.
- Delete `tests/fixtures/shim/legacy-cli.js`, `scripts/test-legacy.sh`, `scripts/test-legacy.ps1`, the
  `legacy-parity` CI job, and `docs/releasing.md`'s legacy-parity line. `parity-check.sh` stays until the
  release is out, then goes too.

### 2d. Text sweep (must pass before the squash)

```bash
grep -rniE "rabbit|amqp|ssot\.me|dspxml|odxml" src tests scripts docs installers cli.js package.json README.md CLAUDE.md
```

Allowed hits, and only these: the migration page (§2b), the step-10 migration module's legacy-path
constants, the binary aliases (`ssotme`, `aicapture`, `aic`), and the single legacy-commit line in
`README.md`. Everything else is a defect. Help-text typos (`bing`, `buid`) and the `dryRun` bareword quirk
are already fixed in step-09.

## 3. Pre-cutover checklist

- [ ] `dotnet test` green locally; `scripts/ci/*` (the local CI equivalents from step-06) green.
- [ ] `parity-check.sh` clean on macOS + Windows.
- [ ] `npm publish --dry-run --access public` and `npm run test:package` pass; the packed
      `@effortlessapi/cli` tarball runs `effortless`, `ssotme`, `aicapture`, and `aic`.
- [ ] Installers built locally on macOS (arm64) and Windows (x64); the four aliases work;
      `-upgradeCli` from the previous legacy version finds the new release.
- [ ] `docs/cli-reference.md`, `README.md`, and the migration page reviewed.
- [ ] Rulebook `RefactorSteps` all `done` except step-13; `TestCases` all `rebuild-green` or `Skip`-documented.
- [ ] The text sweep in §2d passes.

## 4. Cutover: one commit, one push

1. `git checkout main && git merge --squash effortless-cli` (or `git reset --soft` onto `main` HEAD): a
   single commit titled `Effortless CLI v2: clean REST-only rebuild`, body starts with the legacy commit
   line and links the migration page.
2. **This is the first and only push of the rebuild.** CI runs on it. If CI is red, fix forward on `main`
   in small commits; do not amend the pivot commit after it is public.
3. `workflow_dispatch` `build-installers.yml` on a prerelease; verify the four assets (this closes the
   deferred step-06 verification).
4. `scripts/release.sh` (stamps the npm-semver-safe UTC version, commits, pushes, creates the GitHub
   release, publishes `@effortlessapi/cli`) → installers build → `update-airtable.yml` advertises it.
5. `npm install -g @effortlessapi/cli@latest`; verify all four aliases print the new stamp.
6. Post-release: `git tag v2-first-release <sha>`; delete `origin/effortless-cli` (it was pushed early in
   the rebuild and is stale); remove `parity-check.sh`; `RefactorSteps.step-13.Status` → `done` in the
   archive copy.
7. From here on: the v2 rhythm. Feature branches, PRs into `main`, CI on PRs, branch protection on `main`.

## 5. Rollback

`legacy/main` still builds and releases the old line; `git revert` of the squash commit restores the tree.
Because the version scheme is date-based there is no version conflict either way.
