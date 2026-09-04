# Step 06 — DevOps: CI matrix, installers, release flow

**Goal:** the branch has the formal PR/CI/release machinery the owner asked for, built from the legacy
workflows (same artifacts, same tag/version scheme) with a real test gate in front of them. Can run in
parallel with Step 5.

Inputs: `DevopsPipelines`, `EntryPoints`, `ProjectFacts` (`version-format-*`, `git-release-tag-format`,
`installer-asset-names`), `TestSuites.RunsInCi/NeedsNetwork`.

## 1. `.github/workflows/ci.yml` (new)

- Triggers: `pull_request` (all branches), `push` to `effortless-cli` and `main`.
- Jobs:
  - `build-test` matrix `ubuntu-latest`, `macos-latest`, `windows-latest`: `actions/setup-dotnet@v4` (8.0.x),
    `actions/setup-node@v4` (20), `dotnet build Effortless.Cli.sln -c Release`, `dotnet test --filter
    "Slow!=true"` (unit + contract + e2e), upload TestResults on failure.
  - `slow` (ubuntu): `dotnet test --filter "Slow=true"`.
  - `generate-check` (ubuntu): `node scripts/generate-from-rulebook.mjs --check` + rulebook validation.
  - `legacy-parity` (ubuntu, until Step 13 removes it): checks out `legacy-final` into a sibling dir, builds
    it, runs the E2E suite in `legacy` mode. Guarantees the safety net itself stays valid.
- Concurrency group per ref; cancel in-progress.

## 2. Native installer workflows

Retain `build-mac.yml` and `build-windows.yml` as the automatic release workflows so the native installer
paths remain independently operable. Each keeps its `release: [prereleased, released]` and
`workflow_dispatch` triggers, skip-if-assets-exist logic, signing secrets and canonical asset names
(`ProjectFacts.installer-asset-names`), with paths updated to `installers/…` and `src/Effortless.Cli`.
Windows jobs pass the target platform explicitly instead of rewriting the build script.

`build-installers.yml` is a manual all-platform convenience workflow for an existing prerelease tag. It
has no release trigger, avoiding duplicate uploads with the retained native workflows. All three installer
workflows rerun generation, build and non-slow tests against the release commit before building assets.

## 3. `.github/workflows/release.yml` and `update-airtable.yml`

Keep as they are (PR-merge → draft release with the PR body; push to main → Airtable `CLIVersions` row),
fixing paths (`scripts/ci/add_latest_version_to_airtable_db.py`). Document the required secrets in
`docs/releasing.md`: `AIRTABLE_PAT`, `SSOT_BASE_ID`, `APPCERTIFICATE`, `DEVCERTIFICATE`, `CERT_PASSWORD`,
`DEV_INS_KEYCHAIN_ID`, `DEV_APP_KEYCHAIN_ID`, `APPLEID`, `NOTARY_PASSWORD`.

## 4. `scripts/release.sh` (from `release-cli.sh`)

Stamp the npm-semver-safe UTC version `yyyy.(M*100+d).(H*100+m)` into `package.json`, commit
`Release v<version>`, push, create the GitHub release, and publish the public
`@effortlessapi/cli` package. Add guards (`keep-modified`): refuse on a dirty tree, refuse unless on
`main`, require npm authentication, run `dotnet test Effortless.Cli.sln` plus the package smoke test
first, and support a `--dry-run` flag that prints what it would do. Print
`npm install -g @effortlessapi/cli` on success. `devops-release-dry-run` and
`devops-npm-package` cover the guards and distributable.

## 5. PR hygiene

- `.github/pull_request_template.md`: what changed, rulebook rows touched, tests added, `-help` diff if
  options changed.
- `CODEOWNERS` (owner), branch-protection recommendation in `docs/releasing.md` (require `ci.yml` jobs,
  squash merges only, linear history).
- `docs/releasing.md`: the whole flow end-to-end (PR → squash → `release.sh` → installers → Airtable →
  `-upgradeCli` sees it).

**Done when:** CI green on the branch for all jobs; `workflow_dispatch` of `build-installers.yml` produces
the four assets on a test prerelease; `scripts/release.sh --dry-run` works; `RefactorSteps.step-06.Status`
→ `done`.
