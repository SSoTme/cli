# Effortless CLI — clean REST-only rebuild: the plan

This directory is the working plan for turning the legacy `ssotme` CLI (RabbitMQ era) into a clean,
formally maintained `effortless` CLI that speaks **only** the REST transpiler protocol, with byte-level
identical behavior everywhere the protocol change does not force otherwise.

**The single source of truth is the rulebook:** [`effortless-rulebook/effortless-rulebook.json`](../../effortless-rulebook/effortless-rulebook.json).
Everything below is derived from, or points into, that file. If this document and the rulebook ever
disagree, the rulebook wins — fix the rulebook, then regenerate.

| Fact | Value |
|---|---|
| Legacy final commit | `a8f0f320f4417c58fa931cc4bc8164f79cbbbd97` — tag `legacy-final`, branch `legacy/main` |
| Legacy final version | `2026-06-09.06.13` |
| Rebuild branch | `effortless-cli` (this branch) — squash-merged into `main` as one pivot commit at the end |
| Rulebook authored | 2026-08-30 from the legacy source + the tool-side `CLIClassLibrary` + the live `cli-cloud-bridge` |

## How the rulebook is organized

| Table | What it answers |
|---|---|
| `Dispositions` | The seven fates (keep / keep-modified / drop-rabbit / drop-dead / drop-dspxml / drop-legacy / review-*) and which ones need the owner's confirmation |
| `CliOptions` (65 rows) | Every Plossum option: verbatim help text, aliases, bareword verbs, how it is handled, its disposition, **and test coverage** (`CountOfTestCases`, `IsRetainedButUntested`) |
| `DispatchRules` (50) | The exact if/else precedence of `ParseCommand` + `TranspileProject` — the request-handling state machine |
| `LifecyclePhases` / `LifecycleStates` / `StateTransitions` | The process lifecycle, the build loop and the clean loop as a state graph, with the legacy method and the proposed new home for each state |
| `ToolResolutionRules` / `RetryRules` | How a tool name becomes a URL + version label; the HTTP retry matrix |
| `WirePayloadFields` | The REST wire contract (request camelCase / response), with evidence of which fields live tools actually consume |
| `HttpEndpoints` | Every remote endpoint with a liveness probe from 2026-08-30 |
| `ConfigFiles` / `ProjectFileFields` | Every file the CLI touches; the exact `effortless.json` serialization contract |
| `UserMessages` | The console strings tests assert on (goldens) |
| `SourceModules` (55) | The legacy → new move map for every file/dir |
| `Dependencies`, `EnvVariables`, `EntryPoints`, `DevopsPipelines`, `ExitCodes`, `ProjectFacts` | The remaining constants of the system |
| `TestSuites` / `TestCases` (~190) | The comprehensive test plan (Given/When/Then, priority, interactive, slow) |
| `RefactorSteps` | The pieces of the puzzle (this plan) |

Query it, don't read it whole — e.g.

```bash
jq '.CliOptions.data[] | select(.Disposition|startswith("review")) | {CliOptionId, Disposition, DispositionReason}' effortless-rulebook/effortless-rulebook.json
jq '.TestCases.data[] | select(.Priority=="P0") | .TestCaseId' effortless-rulebook/effortless-rulebook.json | wc -l
jq '.SourceModules.data[] | select(.Disposition=="keep-modified") | {LegacyPath, NewPath, Notes}' effortless-rulebook/effortless-rulebook.json
```

## The steps

| Step | Title | Owner | Status | Instructions |
|---|---|---|---|---|
| 00 | Freeze the legacy line and author the rulebook | fable | **done** | this file |
| 01 | Characterization test harness against the **legacy** binary | opus | ready | [step-01-characterization-tests.md](step-01-characterization-tests.md) |
| 02 | New solution skeleton + ported core library | opus | ready (after 01) | [step-02-core-library.md](step-02-core-library.md) |
| 03 | Ported CLI: options, dispatcher, resolver, REST client | opus | ready (after 02) | [step-03-cli-port.md](step-03-cli-port.md) |
| 04 | Cut the legacy tree and finish the repo shape | opus | ready (after 03) | [step-04-repo-cutover.md](step-04-repo-cutover.md) |
| 05 | Rulebook-driven generation | opus | ready (after 04) | [step-05-rulebook-generation.md](step-05-rulebook-generation.md) |
| 06 | DevOps: CI matrix, installers, release flow | opus | ready (after 04, parallel with 05) | [step-06-devops.md](step-06-devops.md) |
| 07 | Resolve open decisions and harden | opus | **blocked on decisions below** | [step-07-decisions-and-hardening.md](step-07-decisions-and-hardening.md) |
| 08 | Parity check and squash-merge cutover | opus | blocked (after 07) | [step-08-parity-and-cutover.md](step-08-parity-and-cutover.md) |

Ground rules that apply to every step:

1. **Tests first, behavior second.** Step 1 pins the legacy behavior in a black-box suite. From Step 2 on,
   nothing ships that turns a `legacy-green` test red unless the rulebook row is `keep-modified` and the
   test asserts the documented new behavior.
2. **The rulebook is edited, never bypassed.** Adding an option, message, file or test means adding a
   row. From Step 5 on, `CliOptions.g.cs`, `BarewordVerbs.g.cs`, `docs/cli-reference.md` and the test
   manifest are generated and CI fails on drift.
3. **Every commit builds and tests green** on the branch (`dotnet build` + `dotnet test`). Small PRs into
   `effortless-cli`; the final squash into `main` is the only large commit.
4. **Never invent a publish procedure.** Releases go through `scripts/release.sh` (Step 6), which is the
   legacy `release-cli.sh` with guards.
5. **Do not touch `main` or `legacy/main`.**

## <a name="decisions"></a>Open decisions for the owner (drive Step 7)

Each decision maps to rows with `Disposition` = `review-drop` / `review-keep` in the rulebook. The
recommendation is encoded there; flip the row (and `NeedsUserConfirmation` on the Disposition) when you
decide. Until then Steps 1–6 proceed under the recommendation.

| # | Decision | Recommendation | Rulebook rows |
|---|---|---|---|
| D1 | **Seeds subsystem** (`-listSeeds`, `-cloneSeed`, `ssotme-seed.json` token replacement that runs on *every* project load, `~/.ssotme/seed_cache`, `RepositoryManager`, `PluralizeService`) | **Drop.** Pre-dates rulebook-first bootstrapping (effortless-init / effortless-demo-app skills). | `CliOptions.listSeeds/cloneSeed`, `SourceModules.extensions-replacement/extensions-repo`, `ConfigFiles.seed-*`, `HttpEndpoints.github-seed-repos/airtable-meta` |
| D2 | **`-buildOnTrigger`** Airtable change-watch loop (host still alive) | **Drop.** Watch/rebuild now lives in `effortless-rulebook-editor`; the bridge URL is hardcoded. (`-copilotConnect` is dropped regardless — hardcoded `"test"` tenant.) | `CliOptions.buildOnTrigger`, `HttpEndpoints.airtable-bridge-check` |
| D3 | **Auth bridge short-circuit.** In a8f0f32 `InvokeToolAndGetOutput` returns `null` unconditionally, so `login`, `projectLogin`, `plan` and the `info` plan lookup are non-functional ("No response from auth service"). | **Re-enable the bridge call for the explicit user commands only** (login / projectLogin / subscription / token refresh); keep the build path 100% bridge-free (no quota, no project registration). | `CliOptions.authenticate/projectLogin/subscription`, `HttpEndpoints.bridge-auth`, `TestCases` with Status `blocked-by-decision` |
| D4 | **pip install path** (`setup.py`, `ssotme/`, `setup.yml`) | **Drop.** npm + MSI + PKG cover every platform; the pip path builds .NET at install time and manages a private SDK. `ssotme/entitlements.plist` moves to `installers/macos/` regardless. | `EntryPoints.pip`, `DevopsPipelines.pip-ci`, `SourceModules.ssotme-py` |
| D5 | **npm shim argument handling.** Legacy `cli.js` joins argv into one string (loses quoting, makes `init force` impossible) and ignores the child exit code. | **Fix both** (pass argv through; propagate exit code). Marked `keep-modified`. | `EntryPoints.npm-shim`, `SourceModules.cli-js`, tests `shim-*` |
| D6 | **Child-process builds** (`buildAll`, `cleanAll`, nested-project parent build) spawn `effortless` **from PATH**. | **Spawn the current executable by path** instead (same console output). Marked `keep-modified`. | `EnvVariables.PATH`, `LifecycleStates.B01/B08/C05` |
| D7 | `-runAs` (alternate `~/.ssotme/ssotme.<user>.key`) | Keep (cheap, coherent with `-setAccountAPIKey`). | `CliOptions.runAs` |
| D8 | Naming: npm package stays `ssotme`, repo stays `EffortlessAPI/cli`, assembly becomes `Effortless.Cli`, all four binary aliases stay. | Confirm. | `ProjectFacts.npm-package-name/binary-aliases/assembly-name` |
| D9 | `-updateUrls` (bridge-v2 host no longer resolves) | Drop as dead; `-refreshTools` / `-upgrade` supersede it. | `CliOptions.updateUrls` |
| D10 | Keep **Plossum.CommandLine.Core** as the parser. Its tokenizer defines how `effortless.json` `CommandLine` strings parse; swapping it would change behavior. | Keep (not in scope). | `Dependencies.Plossum.CommandLine.Core` |

Two documentation discrepancies found while authoring (fix in Step 4):

- The repo `CLAUDE.md` gotcha #2 says a build with no hard pin "resolves its version from `LastVersionUsed`". In a8f0f32 it does **not** — `GetPinnedVersionForTool` returns hard pins only and `LastVersionUsed` is informational (the `[update available]` label branch is unreachable). The rulebook documents the code.
- `README.md` documents `pip install` and the auth0 key-file flow; both are dead.

## Target repo shape (after Step 4)

```
effortless-cli/
├── effortless-rulebook/effortless-rulebook.json     # SSoT
├── effortless.json                                  # this repo's own pipeline (generator + rulespeak)
├── src/
│   ├── Effortless.Cli/                              # exe: Program.cs, Effortless.Cli.csproj
│   └── Effortless.Cli.Core/                         # library
│       ├── CliVersion.cs                            # synced from package.json by cli.js / installers
│       ├── Options/      CliOptions.g.cs, BarewordVerbs.g.cs, CliArgumentParser.cs, CliInvocation.cs
│       ├── Commands/     CommandDispatcher.cs, TranspileCommand.cs, BuildCommand.cs, CleanCommand.cs,
│       │                 InstallCommand.cs, UninstallCommand.cs, ProjectCommands.cs, ToolUrlCommands.cs,
│       │                 VersionCommands.cs, AuthCommands.cs, InfoCommand.cs, UpgradeCliCommand.cs, ExecuteCommand.cs
│       ├── Project/      EffortlessProject.cs, ProjectTranspiler.cs, ProjectSetting.cs, ProjectLocator.cs,
│       │                 ProjectFileStore.cs, BuildRunner.cs, BuildErrorLog.cs, CleanRunner.cs,
│       │                 EmptyFolderPruner.cs, ChildProcessRunner.cs
│       ├── Transpile/    TranspilePayload.cs, TranspileClient.cs, RetryPolicy.cs, ToolResolver.cs,
│       │                 RemoteToolsIndex.cs, CloudBridgeClient.cs, LogEntry.cs, VersionKey.cs
│       ├── FileSets/     FileSet.cs, FileSetFile.cs, FileSetXml.cs, FileSetWriter.cs, FileSetCleaner.cs,
│       │                 InputFileSetLoader.cs, ZfsLedger.cs, GZip.cs
│       ├── Config/       UserConfigDir.cs, KeyFile.cs, EnvFile.cs, ToolUrls.cs, JwtStore.cs, CredentialResolver.cs
│       ├── Auth/         MagicLinkAuth.cs
│       ├── Updates/      CliUpdater.cs, GitHubReleases.cs
│       ├── Console/      CliLog.cs, ToolLogPrinter.cs
│       └── Text/         NameHelpers.cs
├── tests/
│   ├── Effortless.Cli.Tests/                        # unit + contract (xunit)
│   ├── Effortless.Cli.E2E/                          # black-box harness + mock tool/bridge server (xunit)
│   └── fixtures/                                    # projects, index json, tool responses, goldens
├── installers/windows/ , installers/macos/
├── scripts/  release.sh, generate-from-rulebook.mjs, parity-check.sh, test-legacy.sh, ci/
├── docs/     cli-reference.md (generated), refactor-plan/, rulespeak/ (generated)
├── .github/workflows/  ci.yml, build-installers.yml, release.yml, update-airtable.yml
├── cli.js, package.json, Effortless.Cli.sln, README.md, CLAUDE.md, LICENSE, .gitignore
```

## Session protocol for Opus

Start each session with:

```bash
git checkout effortless-cli && git pull
cat docs/refactor-plan/README.md            # this file
cat docs/refactor-plan/step-0N-*.md         # the step you are on
jq '.RefactorSteps.data[] | {RefactorStepId, Status}' effortless-rulebook/effortless-rulebook.json
```

Finish each session by: updating the `Status` of the rows you touched in the rulebook (`RefactorSteps`,
`TestCases`), committing with a `step-0N:` prefix, and pushing the branch.
