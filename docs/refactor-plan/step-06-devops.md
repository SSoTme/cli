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
  - `legacy-parity` (ubuntu, until Step 8 removes it): checks out `legacy-final` into a sibling dir, builds
    it, runs the E2E suite in `legacy` mode. Guarantees the safety net itself stays valid.
- Concurrency group per ref; cancel in-progress.

## 2. `.github/workflows/build-installers.yml`

Merge `build-mac.yml` + `build-windows.yml` into one workflow with four jobs (mac arm64 / x86_64, win x64 /
arm64). Same triggers (`release: [prereleased, released]`, `workflow_dispatch`), same "skip if the release
already has N assets" logic, same secrets, same asset names (`ProjectFacts.installer-asset-names`), paths
updated to `installers/…` and `src/Effortless.Cli`. Add `needs`-style gating on `ci.yml` success for the
release commit (use `workflow_run` or re-run the test job first).

## 3. `.github/workflows/release.yml` and `update-airtable.yml`

Keep as they are (PR-merge → draft release with the PR body; push to main → Airtable `CLIVersions` row),
fixing paths (`scripts/ci/add_latest_version_to_airtable_db.py`). Document the required secrets in
`docs/releasing.md`: `AIRTABLE_PAT`, `SSOT_BASE_ID`, `APPCERTIFICATE`, `DEVCERTIFICATE`, `CERT_PASSWORD`,
`DEV_INS_KEYCHAIN_ID`, `DEV_APP_KEYCHAIN_ID`, `APPLEID`, `NOTARY_PASSWORD`.

## 4. `scripts/release.sh` (from `release-cli.sh`)

Identical stamping (`date -u +"%Y-%m-%d.%H.%M"` into `package.json`), commit `Release v<version>`, push,
`gh release create v<version>`. Add guards (`keep-modified`): refuse on a dirty tree, refuse unless on
`main`, run `dotnet test Effortless.Cli.sln` first, `--dry-run` flag that prints what it would do. Keep the
"then `npm install -g .`" instruction in its output. `devops-release-dry-run` test covers the guards.

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
