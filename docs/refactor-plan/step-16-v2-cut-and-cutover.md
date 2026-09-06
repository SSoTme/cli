# Step 16 — Parity check, v2 clean cut, and single-commit cutover

**Goal:** prove the rebuild is behaviorally identical to `legacy-final` except for the documented
`keep-modified` differences, strip every trace of v1 that is not the migration story out of the shipping
tree, then land v2 on `main` as **one squash commit** with **one push**.

This step was `step-08`, then `step-13`. It is last so the v2 feature steps (09–15) ship inside the same single
commit. Until this step, **nothing is pushed**; the branch is polished locally.

## 1. `scripts/parity-check.sh`

1. Build the rebuild (`dotnet build Effortless.Cli.sln -c Release`).
2. Check out `legacy-final` into `../effortless-cli-legacy` (git worktree) and build it.
3. Run the E2E suite twice — `EFFORTLESS_CLI_MODE=legacy` against the legacy dll and `rebuild` against the
   new dll — with `EFFORTLESS_RECORD_GOLDENS=1` into two separate golden dirs.
4. Diff the two golden dirs. The only allowed differences are the ones enumerated in
   `docs/refactor-plan/parity-allowlist.txt` (generated from the rulebook: every `keep-modified` row's test
   ids, the removed proxy retry lines, and the step-09/10/14 renames and removals). Exit non-zero on anything else.
5. Print a summary table: tests run, identical, allowed-different, unexpected.

Run it on macOS and Windows (the installers' platforms). Fix or document every unexpected difference.

## 2. The v2 clean cut

Owner decision (2026-09-04): v2 must not carry v1's explanations. A reader of the shipping tree should
learn how v2 works, not how v1 used to. The migration story lives in exactly one page.

### 2a. Rulebook cut (owner decision 2026-09-06: delete, do not archive)

- **Delete** every `drop-*` row (`CliOptions`, `SourceModules`, `HttpEndpoints`, `EntryPoints`,
  `DevopsPipelines`), the `legacy-*` and `dead` `OptionCategories`, the `drop-*` and `review-*`
  `Dispositions`, and every `Description`/`Notes`/`HandlerNotes`/`DispositionReason` sentence that explains
  RabbitMQ-, DSPXML- or ODXML-era behaviour. There is no `archive/` copy: git history is the archive.
  The shipping `effortless-rulebook.json` describes v2 only, and the `Disposition` column itself goes
  (every remaining row is v2 by definition).
- `RefactorSteps` and `docs/refactor-plan/` are **deleted** in the cutover commit (they are recoverable
  from git). `README.md` keeps one sentence pointing at the migration page and the legacy commit.
- `ToolResolutionRules`, `ConfigFiles`, `LifecycleStates`, `UserMessages`, `WirePayloadFields`: state the
  v2 rule only; no "was", "formerly", "legacy", "RabbitMQ", "DSPXML", "ODXML".
- Unused legacy `effortless.key` fields are removed from the v2 key-file format.

### 2b. Generated docs

- Remove the **Removed in the rebuild** appendix from `docs/cli-reference.md` (generator change).
- Add `docs/migrating-from-ssotme-v1.md`: one page. Allowed to say RabbitMQ once. Covers: package/binary
  names, `~/.ssotme` → `~/.effortless` (automatic), `ssotme.json` → `effortless.json` (automatic), removed
  options and their replacements, the legacy commit reference.

### 2c. Tests and scripts

- Delete `wire-legacy-request-compat` and `tests/fixtures/wire/legacy-request.json`.
- Delete `tests/fixtures/shim/legacy-cli.js`, `scripts/test-legacy.sh`, `scripts/test-legacy.ps1`, the
  `legacy-parity` CI job, `docs/releasing.md`'s legacy-parity line, and every `Behavior.IsLegacy` /
  `EFFORTLESS_CLI_MODE=legacy` branch in the E2E harness once the parity run is recorded. Nothing legacy
  survives in `tests/`. `parity-check.sh` stays until the release is out, then goes too.

### 2d. Text sweep (must pass before the squash)

Owner rule (2026-09-04, sharpened 2026-09-06): the `ssotme` name is removed from everything except the
`ssotme://` protocol and the binary alias, plus one mention that this was formerly the SSoT.me CLI.
RabbitMQ, AMQP, DSPXML and **ODXML** get zero hits anywhere in the shipping tree (rulebook included);
at most one line in the migration page / changelog may say the transport used to be RabbitMQ. ODXML is
no longer a formal part of the model.

```bash
grep -rniE "rabbit|amqp|ssot\.me|ssotme|dspxml|odxml" src tests scripts docs installers cli.js package.json README.md CLAUDE.md .github
```

Allowed hits, and only these:

- the `ssotme://` protocol scheme wherever the CLI parses or documents it;
- the binary aliases `ssotme`, `aicapture`, `aic` (package.json `bin`, installers, one README line);
- the single "formerly the SSoT.me CLI" line in `README.md` and `docs/releasing.md`;
- the step-10 migration module's legacy path constants (`~/.ssotme`, `.ssotme/`, `ssotme.json`,
  `ssotme.key`, `ssotme-seed.json`, `ssotme.env`) and their tests;
- the default seed source account `ssotme` (it is a GitHub organization name, step 14);
- the migration page (§2b) and the legacy-commit line, which may contain the single RabbitMQ mention.

Everything else is a defect, including any `rabbit`, `amqp`, `dspxml` or `odxml` hit in
`effortless-rulebook/effortless-rulebook.json`. Help-text typos (`bing`, `buid`) and the reserved-word parsing quirks are
already fixed in step-09.

## 3. Pre-cutover checklist

- [ ] `dotnet test` green locally; `scripts/ci/*` (the local CI equivalents from step-06) green.
- [ ] `parity-check.sh` clean on macOS + Windows.
- [ ] `npm publish --dry-run --access public` and `npm run test:package` pass; the packed
      `@effortlessapi/cli` tarball runs `effortless`, `ssotme`, `aicapture`, and `aic`.
- [ ] Installers built locally on macOS (arm64) and Windows (x64); the four aliases work;
      `-upgradeCli` from the previous legacy version finds the new release.
- [ ] `docs/cli-reference.md`, `README.md`, and the migration page reviewed.
- [ ] Rulebook `RefactorSteps` all `done` except step-16; `TestCases` all `rebuild-green` or `Skip`-documented.
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
   the rebuild and is stale); remove `parity-check.sh`; `RefactorSteps.step-16.Status` → `done` in the
   archive copy.
7. From here on: the v2 rhythm. Feature branches, PRs into `main`, CI on PRs, branch protection on `main`.

## 5. Rollback

`legacy/main` still builds and releases the old line; `git revert` of the squash commit restores the tree.
Because the version scheme is date-based there is no version conflict either way.

## Pre-cut fix (found and fixed 2026-09-06)

Script-shape local tools stamped every output `AlwaysOverwrite=true`, so a script could not emit a
write-once file. Fixed in step 12's code the same day: scripts declare per-file modes in
`effortless-overwrite-modes.json`, undeclared files carry no node (protocol default). Covered by
`local-tool-script-overwrite-modes`. The invariant is now in `CLAUDE.md`.
