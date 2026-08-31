# Step 03 — Ported CLI: options, dispatcher, resolver, REST client

**Goal:** `src/Effortless.Cli` builds `Effortless.Cli.dll`; with `EFFORTLESS_CLI_UNDER_TEST` pointing at it
and `EFFORTLESS_CLI_MODE=rebuild`, the **entire Step 1 suite is green** (except rows whose `Status` is
`blocked-by-decision` or `planned-step-03a`), plus the `contract-wire` tests. This is the largest step; split it into three
PRs (3.1 options + dispatch, 3.2 resolution + index + tool URLs + upgrades, 3.3 transpile client + output +
auth/info/updates) if a single session cannot land it.

Inputs: `CliOptions`, `DispatchRules`, `LifecycleStates`/`StateTransitions`, `ToolResolutionRules`,
`RetryRules`, `WirePayloadFields`, `UserMessages`, `ExitCodes`, `HttpEndpoints`, `ConfigFiles`.

## 1. Options (`Options/`)

- `CliOptions.g.cs` — **hand-write it in this step exactly as Step 5 will generate it**: a class with the
  Plossum `[CommandLineManager(ApplicationName = "SSoTme CLI", Copyright = "Copyright 2026, EffortlessAPI.com",
  Description = @"-p description=\n\nSYNTAX: ssotme {command} [...{additional_args}] [options]\nOptions")]`
  attribute (verbatim from `ProjectFacts`), and one `[CommandLineOption(Description = <HelpText>, MinOccurs = 0,
  Aliases = <Aliases>)]` property per `CliOptions` row whose `IsRetained` is true, in rulebook order, with
  the row's `ValueType` (`bool`, `string`, `int`, `List<string>`). Help text and aliases verbatim.
- `BarewordVerbs.g.cs` — `IReadOnlyDictionary<string, Action<CliOptions>>` built from `BarewordForms`
  (retained rows). Match exactly like `FixParameters`: lowercase the first remaining argument, look it up
  **against the verbs as written** (so `dryRun` and `pullAll` never match — the legacy quirk), set the
  option, and for `viewUrl`/`setUrl`/`removeUrl` copy the next argument into the string option. Every
  matched verb sets `transpiler = args[1]` and removes the verb. Step 03A extends the same generated
  string-option path to copy the query for `searchTools`.
- `CliArgumentParser.cs` — wraps Plossum: `Parse(string[] argv)` and `Parse(string commandLine)`; returns
  `CliInvocation { Options, RemainingArguments, HasErrors, ErrorText, UsageHeader, UsageOptions }`. Keep
  the `GetSafeHelpWidth` logic (80 when redirected).
- `CliInvocation.cs` — the mutable per-invocation state that replaces the handler's fields: raw tool arg,
  resolved `TargetUrl`, `Transpiler` (sanitized), `Account`, `ResolvedVersionKey/Url/Label/ToolName`,
  `Project`, `InputFileSet` + XML, `Jwt`, `SuppressTranspile`, `ParseResult`, `IsBuildOperation`,
  `SuppressVersionLabel`, `SkipRemoteToolsLookup`, `SuppressTranspilerErrorOutput`.

## 2. Dispatcher (`Commands/CommandDispatcher.cs`)

Reproduce `DispatchRules` **in SortOrder**, phase `arg-normalization` → `tool-resolution` →
`management-dispatch` → `project-load` → `input-load` → `credential-resolution` → `transpile-dispatch` →
`project-update` → `shutdown`. Concretely:

```
int Run(CliInvocation inv):
  P00..P04  parse, bareword, resolve tool (ToolResolver), positional params
  P05..P10  help / info / version / init / parser errors / management flags   -> may return early
  P11       load project (ProjectLocator) or fail with UserMessages.no-project / cwd-missing
            inject ProjectSettings; auto-build-for-install; InputFileSetLoader; CredentialResolver; .zfs input
  T00       JwtStore.Resolve (env file first)
  T01..T36  exactly the legacy if/else-if chain (one Command class per family, but the ORDER lives here)
  F01..F03  finally: soft pin / install / -latest ; EmptyFolderPruner(cwd)
```

`Program.Main` = legacy `Program.Main` verbatim (exception → exit-code mapping in `ExitCodes`).

The build loop needs to run a step's `CommandLine` through the same pipeline with the project preset and
`IsBuildOperation = true`; provide `CommandDispatcher.RunCommandLine(string commandLine, EffortlessProject
project, bool continueOnError)` and hand it to `BuildRunner` (the injection point left in Step 2).
Same for the internal bridge run (`SkipRemoteToolsLookup`, `SuppressVersionLabel`,
`CliLog.SuppressFileLog`, cwd = `~/.ssotme/remote_tools`).

## 3. Tool resolution (`Transpile/ToolResolver.cs`, `RemoteToolsIndex.cs`, `CloudBridgeClient.cs`)

Implement `ToolResolutionRules` R1–R6, R8–R10 (R7 is gone):
- `RemoteToolsIndex`: paths under `UserConfigDir` (`ConfigFiles.remote-tools-*`), `EnsureInitialized`,
  `IsEmpty`, `Resolve(name, pinnedVersion, latest)` → `(url, versionKey, toolName, label)` with the exact
  label rules, `ListVersions`, `Refresh(triggerTitle, triggerDetails)` (prints the `bridge-banner`, writes
  `cli_version`, ensures the `remote_tools` project file, DNS pre-check, runs the bridge via the dispatcher,
  processes `cliUpdateAvailable` → `update_available.json` and `latestBridgeVersion` → `tool_urls.json` +
  `bridge_version_index`, removes the legacy `list-transpilers` key), and the dead-host recovery.
- The "refresh at most once per process, only when the index is empty or the tool is missing" logic.
- `ToolUrls` overrides and the `[user-set]` label; account extraction from `acct/tool`.
- Dead-bridge recovery retries the bootstrap URL once, then fails non-zero. It must not continue with a
  stale index or any other fallback. The 24-hour `R0-catalog-freshness` gate itself lands in Step 03A.
- **Unresolved name:** do not call anything; leave `TargetUrl` null so T36 reports `tool-not-found`
  (`keep-modified`; test `tx-tool-not-found` in rebuild mode asserts no retry lines and a fast exit).

## 4. Transpile client (`Transpile/TranspileClient.cs`, `RetryPolicy.cs`)

- Build the request from `CliInvocation` exactly as `SaveCLIOptions` + `conditionallyPopulateTranspiler` +
  `ProxyRequest` did (see `LifecycleStates.S15`): `CLIInputFileContents = ""`, `TranspileRequest.
  ZippedInputFileSet = gzip(inputXml)`, `Transpiler.Name = transpiler`, `Transpiler.LowerHyphenName =
  SanitizeUrlForFilename(targetUrl)`, `Settings = {}`; `PayloadId` = guid-pair, `SenderId` = guid key.
- POST with `HttpClient.PostAsJsonAsync` (System.Net.Http.Json, Web defaults ⇒ camelCase) so the emitted
  names match `WirePayloadFields` — verify with `wire-request-snapshot`. Timeout = `waitTimeout`.
- Retry loop per `RetryRules` with the exact messages; async task polling (`/task/{id}`, 3000 ms);
  the 15 s boot spinner; `waitForCook` semantics (`Timed out waiting for cook`).
- Response: parse with Newtonsoft (case-insensitive) into `TranspilePayload`; the name fix-ups (`S19`);
  `ToolLogPrinter` + error-log promotion (`S20`); `CLIDebug` echo; the `DEBUG`-only ZFS content validation.
- Output apply: `ZfsLedger.CleanFileSet` / `SaveFileSet` (Step 2) — `S21`–`S23`.

## 5. Everything else in `Commands/`

- `ProjectCommands` (init incl. the `.gitignore` / `effortless.env` / rulebook scaffolds, describe,
  describeAll, listSettings, addSetting, removeSetting), `InstallCommand` (install, URL install, dryRun,
  auto-build), `UninstallCommand` (prompt), `ExecuteCommand`, `BuildCommand` (BuildErrorLog begin/finish),
  `CleanCommand` (three branches), `ToolUrlCommands` (set/view/list/remove), `VersionCommands`
  (listVersions, refreshTools, upgrade, upgradeAll, `-latest` handling in F02), `AuthCommands` (login,
  projectLogin, logout, subscription — via `Auth/MagicLinkAuth` + `CloudBridgeClient.InvokeAndGetOutput`;
  D3 re-enables bridge calls for these explicit user commands only, while the build/transpile path remains
  bridge-free), `InfoCommand`, `UpgradeCliCommand` (`Updates/`).
- Drop: discuss, legacy, updateUrls, localGuide, checkResults, createDocs, seeds (D1 default), buildOnTrigger
  (D2 default), copilotConnect, addTranspiler, deleteTranspiler, keyFile, repoUrl, betaRepo, skipBuild.

## 6. Exe project

`src/Effortless.Cli/Effortless.Cli.csproj`: `OutputType Exe`, `AssemblyName Effortless.Cli`, `<Version>`
placeholder synced from `package.json` (Step 4 wires `cli.js`), reference Core. `CliVersion.cs` in Core:
`public static class CliVersion { public const string Value = "2026-06-09.06.13"; }` — same literal the
installers/shim regex-replace (`public const string Value = ".*?";`).

## 7. Verification

```bash
dotnet build Effortless.Cli.sln -c Release
EFFORTLESS_CLI_UNDER_TEST=$PWD/src/Effortless.Cli/bin/Release/net8.0/Effortless.Cli.dll EFFORTLESS_CLI_MODE=rebuild dotnet test tests/Effortless.Cli.E2E
dotnet test tests/Effortless.Cli.Tests
```

Work suite by suite in the same order as Step 1; commit whenever a suite turns green. When a test fails,
the rulebook decides who is right: if the legacy behavior is `keep`, fix the port; if it is `keep-modified`,
the test must already have a `Behavior.IsLegacy` branch — if it does not, add the branch **and** the
rulebook `Description` explaining the change.

**Done when:** rebuild-mode E2E green (minus blocked-by-decision and planned-step-03a), unit + contract
green, legacy-mode E2E still green, implemented `TestCases.Status` → `rebuild-green`, deferred rows stay
`planned-step-03a`, and `RefactorSteps.step-03.Status` → `done`.
