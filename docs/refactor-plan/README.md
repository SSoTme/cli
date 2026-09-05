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
| `CliOptions` (67 rows) | Every Plossum option: verbatim help text, aliases, bareword verbs, how it is handled, its disposition, **and test coverage** (`CountOfTestCases`, `IsRetainedButUntested`) |
| `DispatchRules` (50) | The exact if/else precedence of `ParseCommand` + `TranspileProject` — the request-handling state machine |
| `LifecyclePhases` / `LifecycleStates` / `StateTransitions` | The process lifecycle, the build loop and the clean loop as a state graph, with the legacy method and the proposed new home for each state |
| `ToolResolutionRules` / `RetryRules` | How freshness is proven, how a tool name becomes a URL + version label, and the HTTP retry matrix |
| `WirePayloadFields` | The REST wire contract (request camelCase / response), with evidence of which fields live tools actually consume |
| `HttpEndpoints` | Every remote endpoint with a liveness probe from 2026-08-30 |
| `ConfigFiles` / `ProjectFileFields` | Every file the CLI touches; the exact `effortless.json` serialization contract |
| `UserMessages` | The console strings tests assert on (goldens) |
| `SourceModules` (55) | The legacy → new move map for every file/dir |
| `Dependencies`, `EnvVariables`, `EntryPoints`, `DevopsPipelines`, `ExitCodes`, `ProjectFacts` | The remaining constants of the system |
| `TestSuites` / `TestCases` (226) | The comprehensive test plan (Given/When/Then, priority, interactive, slow), including the post-characterization Step 03A cases |
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
| 01 | Characterization test harness against the **legacy** binary | opus | **done** | [step-01-characterization-tests.md](step-01-characterization-tests.md) |
| 02 | New solution skeleton + ported core library | opus | **done** | [step-02-core-library.md](step-02-core-library.md) |
| 03 | Ported CLI: options, dispatcher, resolver, REST client | opus | **done** | [step-03-cli-port.md](step-03-cli-port.md) |
| 03A | **Automatic tool freshness and catalog discovery (new behavior)** | opus | **done** | [step-03a-tool-freshness-and-discovery.md](step-03a-tool-freshness-and-discovery.md) |
| 04 | Cut the legacy tree and finish the repo shape | opus | **done** | [step-04-repo-cutover.md](step-04-repo-cutover.md) |
| 05 | Rulebook-driven generation | opus | **done** | [step-05-rulebook-generation.md](step-05-rulebook-generation.md) |
| 06 | DevOps: CI matrix, installers, release flow | opus | code landed; live-CI verification deferred to 13 (no push before then) | [step-06-devops.md](step-06-devops.md) |
| 07 | Resolve open decisions and harden | opus | **done** | [step-07-decisions-and-hardening.md](step-07-decisions-and-hardening.md) |
| 09 | Option taxonomy, verb families, and the v2 option decisions (D12–D27) | opus | **done** | [step-09-option-taxonomy.md](step-09-option-taxonomy.md) |
| 10 | `.ssotme` → `.effortless` with in-field migration (option A); rebrand installers | opus | **done** | [step-10-effortless-home-migration.md](step-10-effortless-home-migration.md) |
| 11 | Catalog freshness v2 (refresh on timeout) + searchable catalog | opus | ready (after 07) | [step-11-catalog-freshness-and-search.md](step-11-catalog-freshness-and-search.md) |
| 12 | Local transpiler host: native .NET + node/express fileset handler | opus | ready (after 11) | [step-12-local-transpiler-host.md](step-12-local-transpiler-host.md) |
| 13 | Effortless authentication workload (api.effortlessapi.com) + wire auth commands | opus | ready (after 09; cross-repo) | [step-13-auth-workload.md](step-13-auth-workload.md) |
| 14 | Seeds v2: seed sources, verbs, `effortless-seeds` skill | opus | ready (after 10; cross-repo) | [step-14-seeds-v2-and-skill.md](step-14-seeds-v2-and-skill.md) |
| 15 | Ledger v2: hash manifests replace `.zfs`, readable keys | opus | proposed, non-blocking (may defer to v2.x) | [step-15-ledger-v2.md](step-15-ledger-v2.md) |
| 16 | Parity check, **v2 clean cut**, single-commit cutover (was 08) | opus | blocked (after 14) | [step-16-v2-cut-and-cutover.md](step-16-v2-cut-and-cutover.md) |

## The v2 operating model (decided 2026-09-04)

- **This is v1 → v2 for the world** (internally v51 → v52). The package rename, the transport change, and
  the dependency cut from 16 to 2 are a major version; say so in the release notes and README.
- **Nothing is pushed until step 13.** The branch is massaged, tweaked, and polished locally. Steps 07–12
  all ship inside the same single squash commit on top of `main` HEAD, then one push, then the regular v2
  rhythm (feature branches, PRs, CI on PRs, branch protection).
- **Parity first, then the cut.** Steps 01–08 optimized for byte-identical behavior so the port could be
  proven. Steps 09–13 are where v2 is allowed to differ on purpose: every deliberate difference is a
  `keep-modified` rulebook row with a test, and the clean-cut checklist in step 13 removes v1's
  explanations from the shipping tree. v2 must not carry v1's story except in one migration page.
- **The 54 kept options stay, better explained.** The owner's position: few can be removed, most need
  updating, and they need tiers and a hierarchy so `-help` is not one list of 54. Step 09 encodes the
  owner's per-option reasoning in the rulebook before any code changes.
- **Every step is the same shape.** Rulebook rows (options, rules, messages, files, test cases) →
  `npm run generate` → implement → `dotnet test` green → step status `done`.
- **The `ssotme` name goes away** (D31) except the `ssotme://` protocol, the `ssotme` binary alias, and one
  "formerly the SSoT.me CLI" line. Directories, files, env vars, installers, help text, workflow names
  and docs are all renamed, with in-field migration where state is involved.
- **Cross-repo steps push their own repos only.** Step 13 publishes a tool from
  `api.effortlessapi.com`; step 14 pushes `effortless-skills`. This repo is not pushed before step 16.

Ground rules that apply to every step:

1. **Tests first, behavior second.** Step 1 pins the legacy behavior in a black-box suite. From Step 2 on,
   nothing ships that turns a `legacy-green` test red unless the rulebook row is `keep-modified` and the
   test asserts the documented new behavior. Rows marked `planned-step-03a` are an explicit new-behavior
   exception to legacy characterization; Step 03A must turn all of them `rebuild-green` before Step 4.
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
| D1 | **Seeds subsystem** (`-listSeeds`, `-cloneSeed`, `ssotme-seed.json` token replacement that runs on *every* project load, `~/.ssotme/seed_cache`, `RepositoryManager`, `PluralizeService`) | DO NOT DROP - it needs to be moved into the rulebook as seeds.  These are root or child effortless repositories with a full effortless stack in the root.  In fact - add a ../effortless-skills/ skill - so that this can be more formally supported.  It is driven by finding public repositories under a github account that have an effortless.json file in the root.  After that though - that s a seed, and the clone seed functionality totally still applies conecptually |
| D2 | **`-buildOnTrigger`** Airtable change-watch loop (host still alive) | DO NOT DROP.  This is totally separate from the editor which can ALSO detect build changes - but this is a separate cloud based trigger.  There may be other ways in the fture - but once again, conceptually this should be something a CLI can monitor from the command line without a whole docker instance behhind the scenes.  That is WAY overengineered for a number of use cases for this general purpose parameter. |
| D3 | **Authentication while the service endpoint is not enabled.** | **Confirmed:** retain clean `login`, `projectLogin`, `logout`, and `plan` command surfaces. Login/project-login/plan are explicit non-networking stubs for now; logout still clears local tokens. Tool execution and `buildOnTrigger` remain unauthenticated. Implement the magic-link flow later when the server endpoint is enabled. | `CliOptions.authenticate/projectLogin/subscription`, `HttpEndpoints.bridge-auth`, `TestCases auth-*` |
| D4 | **pip install path** (`setup.py`, `ssotme/`, `setup.yml`) | **Confirmed drop.** npm + native MSI/PKG installers cover distribution without retaining a Python shim or private SDK manager. `installers/macos/entitlements.plist` remains for native codesigning. | `EntryPoints.pip`, `DevopsPipelines.pip-ci`, `SourceModules.ssotme-py` |
| D5 | **npm shim argument handling.** Legacy `cli.js` joins argv into one string (loses quoting, makes `init force` impossible) and ignores the child exit code. | **Fix both** (pass argv through; propagate exit code). Marked `keep-modified`. | `EntryPoints.npm-shim`, `SourceModules.cli-js`, tests `shim-*` |
| D6 | **Child-process builds** (`buildAll`, `cleanAll`, nested-project parent build) spawn `effortless` **from PATH**. | buildAll/cleanAll is actually for running a full build for the project itself, based NOT on the current flder - but rather, behaving as though the effortless -build was being run from the root, sibling to the effortless.json.  This COULD also add a 2nd parameter to indicate that it should do a buildall including sub-effortless projects, which are generally NOT included in a build.| `EnvVariables.PATH`, `LifecycleStates.B01/B08/C05` |
| D7 | `-runAs` (alternate `~/.ssotme/ssotme.<user>.key`) | Keep (cheap, coherent with `-setAccountAPIKey`). | `CliOptions.runAs` |
| D8 | Naming: official npm package is `@effortlessapi/cli`, repo stays `EffortlessAPI/cli`, assembly is `Effortless.Cli`, and all four binary aliases (`effortless`, `ssotme`, `aicapture`, `aic`) stay. The unscoped npm package `effortless` is unrelated. | Confirmed. | `ProjectFacts.npm-package-name/binary-aliases/assembly-name` |
| D9 | `-updateUrls` (bridge-v2 host no longer resolves) | this whole feature needs to be made consistent in it's nameing, but it still needs to support the full list of adding a tool, listing a tool, removing a custom tool url. |
| D10 | Keep **Plossum.CommandLine.Core** as the parser. Its tokenizer defines how `effortless.json` `CommandLine` strings parse; swapping it would change behavior. | Keep (not in scope). | `Dependencies.Plossum.CommandLine.Core` |
| D11 | The new parameter parsing should allow globally for any of the key parameters to support no -, or double hyphen. In other words, either `effortless build` or `effortless -build` or `effortless --build`.  Any of those key words are reserved, and should NEVER be treated as a transpiler.||
| D12 | **Verb families.** | Every project-scoped verb follows `foo` (this folder + children) / `fooLocal` (exactly here) / `fooAll` (as if from the root) / `fooWithSubprojects` (`All` + nested effortless projects, normally excluded). Applies to `build`, `clean`, `describe`. Done 2026-09-05 with two corrections: `describe` was **already** downstream, so only `describeLocal` was new behavior; and `All` **did** walk into nested projects in v1, so `buildAll`/`cleanAll` were narrowed to stop at project boundaries and the traversal moved to `*WithSubprojects`. | step 09 |
| D13 | **Ledger format.** `.zfs` keeps exact bytes for byte-for-byte compare/restore; never leveraged. | Replacing it with a per-tool, per-execution manifest hashing every file actually written is acceptable, same behavior otherwise. Also fix the key naming (`httplocalhost4242tool` → `http-localhost-4242-tool`). Not critical. | step 15 |
| D14 | `-transpilerGroup` | Keep; it is how you run only the postgres steps. | step 09 |
| D15 | `-targetUrl` vs bare URL | Keep both. Tool name + `-targetUrl` is a temporary override that keeps the tool's identity. | step 09 |
| D16 | `setUrl` family naming | `ToolUrl` is canonical (`setToolUrl`, `removeToolUrl`, `viewToolUrl`, `listToolUrls`); `setUrl` was a mistake, kept as hidden alias. | step 09 |
| D17 | **Pinning.** | A step is pinned (to a catalog version or a URL, per project) or it follows HEAD. New `pin` verb; `upgrade` = remove the pin (alias `unpin`); `upgradeAll` = remove all. `latest` removed (an attribute, not a command). Default stays unpinned; pin-on-install not assumed. Done 2026-09-05. Reconciled with step-03A's currentness gate, which used to clear every pin on every build and would have made `-pin` a no-op: a pin the catalog can still resolve is deliberate and survives a build, while a pin that no longer resolves is stale and is still cleared. Only `upgrade`/`upgradeAll` unpin unconditionally. | step 09 |
| D18 | **Auth commands.** | Keep them visible. Publish a real `effortless-auth` workload from the private `api.effortlessapi.com` tools repo, CPLN scale-to-zero after 5 min, always succeeds for now; CLI calls it through the catalog. No usage tracking. Display names `login`/`plan` (aliases `authenticate`/`subscription`) chosen by default; flip if wanted. | step 13 |
| D19 | **Account keys.** `-setAccountAPIKey foo=bar` | Keep. The key is stored separately and injected automatically as a parameter when a tool in that account runs (`effortless foo/whatever -p x=5` also sends `foo=bar`); `-account foo` selects it explicitly. Storage location is fungible (moves to `~/.effortless`). Alias `setAccountKey`. | step 09 / 10 |
| D20 | `acct/tool` extraction | Never fail. If `acct/tool` is not in the catalog, resolve `tool` with `-account acct`. | step 09 |
| D21 | `-output` | Whatever the tool decides; passed as `OutputFileName`. File or directory is the tool's call. | step 09 / 12 |
| D22 | Disabled steps | Add `enable` / `disable` verbs (targeted like `uninstall`). Document all new/changed terms in README and `-help`. | step 09 |
| D23 | `info` / `listSettings` / `describe` | Confirmed split (user state / project settings / steps). `info` also shows catalog age (time since the tool list was last refreshed). | step 09 / 11 |
| D24 | `-buildOnTrigger` | Keep, category `build`. It polls a tiny no-auth/no-DB bridge workload. Verified route (recorded in `HttpEndpoints.airtable-bridge-check`): `GET {bridge}/check?baseId={id}` every 3 s returning `{changed: bool}`, rebuilding after a 10 s quiet period. No `/set` or `/get` route exists in the codebase; that earlier description was wrong. | step 09 |
| D25 | **Seeds.** | Seed sources are an ordered list of GitHub accounts, defaults `ssotme` and `effortlessapi`, with add/remove/list verbs. Write an `effortless-seeds` skill in `../effortless-skills`, push that repo, install it. | step 14 |
| D26 | `-execute` vs local tools | Keep both. `execute` is a shell step with no fileset; local tools are the fileset-aware path. | step 09 / 12 |
| D27 | `-dryRun` | **Remove.** Early steps write what later steps read; "run but do not write" is not a build. Git on a clean tree is the dry run. | step 09 |
| D28 | Project ledger dir | Option A: `<root>/.ssotme/` → `<root>/.effortless/` with an on-load rename. | step 10 |
| D29 | "Completely times out" | DNS failure, connection refused, TLS failure, or no response bytes within `waitTimeout` after the connection retries = workload offline. Refresh the catalog and retry at least once; long-running modes at most once per 10 minutes. | step 11 |
| D30 | Local host runtime | Native .NET host in the CLI. `dotnet` tools are the same shape as published cloud tools; a node/express fileset handler with the same logic ships for node tools; scripts get an env-dir contract. | step 12 |
| D31 | **Global naming.** | `ssotme` survives only as the `ssotme://` protocol, the binary alias, migration constants, the default seed source account, and one "formerly the SSoT.me CLI" line. | step 10 / 16 |
| D32 | `docs/releasing.md` secrets list | Removed. Those secrets are per-workflow (signing, Airtable) and not required to build, test, or publish; each workflow documents its own. | done 2026-09-04 |

The owner also confirmed removal of the legacy Airtable metadata endpoint used
for seed-replacement guessing. This does not remove seed discovery, cloning,
explicit `$key$` replacement, or unauthenticated `buildOnTrigger`.

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
│       │                 ProjectToolFreshness.cs, EmptyFolderPruner.cs, ChildProcessRunner.cs
│       ├── Transpile/    TranspilePayload.cs, TranspileClient.cs, RetryPolicy.cs, ToolResolver.cs,
│       │                 RemoteToolsIndex.cs, CatalogFreshnessPolicy.cs, CloudBridgeClient.cs, LogEntry.cs, VersionKey.cs
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
cat docs/refactor-plan/step-0N*.md          # the step you are on (includes step-03a)
jq '.RefactorSteps.data[] | {RefactorStepId, Status}' effortless-rulebook/effortless-rulebook.json
```

Finish each session by: updating the `Status` of the rows you touched in the rulebook (`RefactorSteps`,
`TestCases`), committing with a `step-0N:` prefix, and pushing the branch.
