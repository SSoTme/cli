# Step 04 — Cut the legacy tree and finish the repo shape

**Goal:** the branch contains only the new repo shape (see README "Target repo shape"); the legacy tree is
gone; the `@effortlessapi/cli` package installs the rebuilt CLI under all four names; installers build
from the new paths; docs and agent guides describe the new world.

Inputs: `SourceModules` (every row: `Disposition` + `NewPath` + `Notes`), `EntryPoints`, `DevopsPipelines`,
`ProjectFacts`, `ConfigFiles.gitignore` (template stays as-is — it is the *project* template, not the repo's).

## 1. Move / delete (one commit per group)

1. `git mv Windows/Installer installers/windows` and `git mv macOS/Installer installers/macos`; move
   `ssotme/entitlements.plist` → `installers/macos/entitlements.plist`; `build-package.sh` →
   `installers/macos/build-package.sh`; `build-installer.ps1` → `installers/windows/build-installer.ps1`.
   Fix every path inside `build.ps1` / `build.sh` (`Windows/CLI` → `src/Effortless.Cli`, `SSoTme.OST.CLI.csproj`
   → `Effortless.Cli.csproj`, the `CLI_VERSION` regex → `CliVersion.cs` `Value` regex, `SSoTme.OST.CLI(.exe)`
   → `Effortless.Cli(.exe)`, `$SOURCE_DIR/entitlements.plist`). Keep the four alias copies, PATH env, MSI
   version encoding, README/LICENSE embedding, notarization.
2. `git rm -r Windows DSPXml ODXML SSoT docs/schema docs/seeds docs/*.xslt docs/*.md docs/images docs/SomeFile.txt
   README.hbrs setup.py MANIFEST.in requirements.txt ssotme .effortless ssotme.json SSoTme-OST-CLI.sln`
   (`ssotme/` only after step 1 above). If D1/D2/D4 flip to keep, port before deleting (their rows say where).
3. `.github/scripts/add_latest_version_to_airtable_db.py` → `scripts/ci/`; workflows are rewritten in Step 6
   but must at least *not reference deleted paths* after this step (fix paths now).
4. Root `effortless.json`: replace with the repo's own project (Step 5 registers the steps; for now
   `effortless -init -name effortless-cli` output with the `SSoTmeProjectId` preserved is fine).
5. `cli.js`: point `solutionPath` at `Effortless.Cli.sln`, `outputPath` at
   `src/Effortless.Cli/bin/Release/net8.0/Effortless.Cli.dll`; sync targets `src/Effortless.Cli/Effortless.Cli.csproj`
   (`<Version>`) and `src/Effortless.Cli.Core/CliVersion.cs` (`Value = "..."`); **pass argv through**
   (`spawn('dotnet', [outputPath, ...process.argv.slice(2)], { stdio: 'inherit' })`) and **propagate the
   exit code** (`child.on('exit', code => process.exit(code ?? 1))`) — D5, `keep-modified`.
6. `package.json`: keep `name`, `bin`, `version` format; `scripts.build` → the new sln; add `test`,
   `test:legacy` (kept until Step 16), `generate` (Step 5). Remove `cloud_bridge_url` (nothing reads it).
7. `.gitignore` per `SourceModules.gitignore-root`.

## 2. Documents

- `README.md` (hand-written, ~150 lines): what it is; install (`@effortlessapi/cli`, development checkout,
  MSI, PKG, `-upgradeCli`);
  quick start (`-init`, `install`, `build`, `clean`); the `-continueOnError` section verbatim from the
  legacy README; **exactly one line** under a "Legacy" heading:
  `The last commit of the original SSoTme/RabbitMQ-era CLI is a8f0f320f4417c58fa931cc4bc8164f79cbbbd97 (tag legacy-final, branch legacy/main).`;
  a pointer to `docs/cli-reference.md` (generated in Step 5) and to `docs/refactor-plan/`; contributing
  (branch → PR → CI green → squash merge → `scripts/release.sh`).
- `CLAUDE.md`: rewrite for the new layout. Keep the two legacy gotchas (`Save()` must not resurrect
  model-owned props; `-upgrade` must clear the pin **and** advance `LastVersionUsed`) but correct gotcha #2's
  wording (no hard pin ⇒ HEAD; `LastVersionUsed` is informational). Add: "edit the rulebook, run
  `npm run generate`, never hand-edit `*.g.cs`"; "run `dotnet test` before every commit"; the release
  procedure (`scripts/release.sh`); the local-tool-URL debugging loop (`-setToolUrl x=http://localhost:PORT`).
- `docs/refactor-plan/` stays (history); mark Step 4 done in the rulebook.

## 3. Verification

```bash
git ls-files | grep -E '^(Windows|DSPXml|ODXML|SSoT|ssotme)/' && echo "LEFTOVERS" || echo clean
npm publish --dry-run --access public
npm run test:package
dotnet test Effortless.Cli.sln            # unit + contract + e2e (rebuild mode is now the default dll)
bash installers/macos/build-package.sh --no-update   # macOS only; unsigned build must at least reach pkgbuild
```

Run the `e2e-shim` suite (`shim-argv-passthrough`, `shim-exit-code`, `shim-version-sync`, `shim-aliases`).

**Done when:** the checks above pass, `git ls-files` matches `SourceModules.NewPath` (+ new files),
`RefactorSteps.step-04.Status` → `done`. Commit `step-04: legacy tree removed; repo reshaped`.
