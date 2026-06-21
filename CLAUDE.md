# Effortless / SSoTme CLI — repo guide for Claude

This repo builds the `effortless` (a.k.a. `ssotme` / `aicapture` / `aic`) CLI. `cli.js` is an npm
shim that builds and runs the bundled .NET 8 binary (`Windows/CLI/...SSoTme.OST.CLI.dll`). All four
`bin` names point at `cli.js`.

## Publishing a new version — ALWAYS do this, never hand-roll it

When you change CLI source and want it released, run the publish script from the repo root:

```bash
cd ~/.effortless/cli
./release-cli.sh          # stamps package.json with a fresh yyyy-mm-dd.hh.mm version,
                          # commits + pushes that to main, and cuts a GitHub release
                          # (the release triggers the MSI + PKG CI builds).
npm install -g .          # upgrade THIS machine's global install to the just-released version.
```

Then verify: `effortless -version` should print the new `yyyy-mm-dd.hh.mm`.

**Rules:**
- **Commit your source fixes FIRST** (the `.cs` files etc.), THEN run `./release-cli.sh`. The release
  script only stamps + commits `package.json`; it does not commit your code changes for you.
- **Do NOT hand-edit the version** in `package.json` or `Windows/CLI/SSoTme.OST.CLI.csproj`.
  `release-cli.sh` owns `package.json`'s version, and `cli.js` auto-syncs the `<Version>` in the
  `.csproj` and `CLI_VERSION` in `SSoTmeCLIHandler.cs` from `package.json` on the next build. If you
  see those two files modified in the working tree after a build, that's the auto-sync — `git checkout`
  them before committing and let the release flow regenerate them, so the version stamp stays consistent.
- `./build-package.sh` builds the local macOS `.pkg` installer only — it does NOT publish. Use
  `release-cli.sh` to publish.
- After any source change, rebuild before testing: `rm -rf Windows/CLI/bin && dotnet build SSoTme-OST-CLI.sln -c Release`,
  then `npm install -g .` (the global symlink already points here, so `cli.js` runs the freshly-built dll).

## Gotchas already fixed here (don't reintroduce)

- **`-upgrade` must persist.** `SSoTmeProject.Save()` merges the on-disk `effortless.json` to preserve
  custom transpiler properties — but it must SKIP model-owned properties (`PinnedVersion`,
  `LastVersionUsed`, etc.). `PinnedVersion` serializes with `IgnoreAndPopulate`, so setting it to `null`
  omits it; if the merge copies the old value back from disk, the unpin is silently undone. Keep the
  `modelOwnedProps` skip-set in `Save()`.
- **`-upgrade` must advance the soft pin.** A build with no hard pin resolves its version from
  `LastVersionUsed` (`GetPinnedVersionForTool` returns it). So `UpgradeSingleTool` / `UpgradeAllTools`
  must set `LastVersionUsed = <resolved head>` in addition to clearing `PinnedVersion`, or the next
  build stays locked to the old version despite the "unpinned — will track latest" message.
