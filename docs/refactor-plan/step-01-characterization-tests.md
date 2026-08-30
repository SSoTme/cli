# Step 01 — Characterization test harness against the LEGACY binary

**Goal:** a black-box xunit suite plus a mock transpiler/bridge server that executes every P0/P1 `e2e-*`
row of `TestCases` against the **unmodified** legacy CLI (`Windows/CLI/bin/Release/net8.0/SSoTme.OST.CLI.dll`)
and is green. This suite is the safety net for every later step; it must not know anything about the
new code. Exclude rows whose `Status` is `planned-step-03a`: those specify newly requested behavior with
no legacy contract and are implemented in the same harness in Step 03A.

**Hard rule:** no changes to any file under `Windows/`, `cli.js`, or `package.json` in this step. If a
legacy behavior looks like a bug, pin it anyway (or record it in the rulebook as `keep-modified` with a
`Description` and assert both behaviors behind the `Behavior.IsLegacy` switch — see §6).

Inputs to read first:

- `effortless-rulebook.json` → `TestSuites`, `TestCases` (filter `SuiteKind == "black-box"`), `UserMessages`
  (`IsGoldenForTests == true`), `WirePayloadFields`, `RetryRules`, `ConfigFiles`, `ProjectFacts`.
- Legacy code paths you are pinning: `SSoTmeCLIHandler.ProxyRequest/StartTranspile` (request + retries),
  `SSOTMEPayload.SaveFileSet/CleanFileSet`, `SSOTMEExtensions_core.SplitFileSetXml/CleanFileSet`,
  `SSoTmeProject.Save/Init/DoRebuild/Clean`, `ProjectTranspiler` ctor.

## 1. Projects and layout

```
tests/
├── Effortless.Cli.E2E/
│   ├── Effortless.Cli.E2E.csproj          # net8.0, xunit, no reference to any CLI project
│   ├── Harness/
│   │   ├── CliUnderTest.cs                # resolves the dll + entry mode from env; runs the process
│   │   ├── Sandbox.cs                     # temp HOME + temp project dir + PATH shim dir
│   │   ├── MockToolServer.cs              # HttpListener: tools + bridge + async tasks
│   │   ├── FileSetXml.cs                  # tiny FileSet XML builder/parser + gzip (test-side copy)
│   │   ├── IndexFixture.cs                # builds ssotme-tools.json pointing at the mock server
│   │   ├── Golden.cs                      # normalization + compare/record
│   │   └── Behavior.cs                    # IsLegacy switch
│   ├── Suites/                            # one file per TestSuite: MetaTests.cs, ProjectTests.cs, ...
│   ├── Goldens/<TestCaseId>.txt           # recorded from the legacy run (normalized)
│   └── TestManifest.g.json                # (Step 5 generates it; hand-seed it now from the rulebook)
└── fixtures/
    ├── projects/                          # ready-made effortless.json trees (copied into the sandbox)
    ├── index/                             # remote index templates (URLs contain {port})
    └── tool-responses/                    # FileSet XML fragments the mock returns
```

Add a `scripts/test-legacy.sh` (and `.ps1`) that builds the legacy solution (`dotnet build
SSoTme-OST-CLI.sln -c Release`) and runs `dotnet test tests/Effortless.Cli.E2E` with
`EFFORTLESS_CLI_UNDER_TEST=<abs path to SSoTme.OST.CLI.dll>` and `EFFORTLESS_CLI_MODE=legacy`.
Add a new solution file `Effortless.Cli.sln` containing only the test project for now (the legacy sln
stays as-is); Step 2 adds the src projects to it.

## 2. `CliUnderTest` — how a test invokes the CLI

- Reads `EFFORTLESS_CLI_UNDER_TEST` (absolute dll path). Default: the legacy dll relative to the repo root
  if it exists, else `src/Effortless.Cli/bin/Release/net8.0/Effortless.Cli.dll`.
- `Run(string[] args, string cwd, string? stdin = null, int timeoutMs = 120_000)` → `CliResult { ExitCode,
  Stdout, Stderr, Combined, Duration }`. Spawn `dotnet <dll> <args...>` (one argv entry per arg — this is
  the native-argv path; the npm shim is covered separately in `e2e-shim`).
- Environment for the child: `HOME` **and** `USERPROFILE` = sandbox home; `PATH` = shim dir + original
  PATH; `SSOTME_CHILD_PROCESS` unset; `DOTNET_CLI_TELEMETRY_OPTOUT=1`; `TERM=dumb`; keep everything else.
  Console colors: the legacy CLI writes ANSI sequences only when attached to a TTY — redirected output is
  plain text; assert on plain text.
- Exit code: legacy returns `-1` → **255 on macOS/Linux, 4294967295 on Windows**. Provide
  `result.Failed` = `ExitCode != 0` and assert with that.
- Timeout kills the process tree and fails the test with the captured output.

### PATH shim dir

Create in the sandbox a `bin/` with executables `effortless`, `ssotme`, `aicapture`, `aic`:
- POSIX: `#!/bin/sh\nexec dotnet "<dll>" "$@"` (chmod +x).
- Windows: `effortless.cmd` etc: `@dotnet "<dll>" %*`.
This is what makes `buildAll` / nested builds (`inst-prefix-strip-aliases`, `build-all`,
`build-nested-parent-root`, `clean-all`, `shim-aliases`) work against the legacy binary, which spawns
`effortless` from PATH.

## 3. `Sandbox`

- `Sandbox.Create()` → temp root under the OS temp dir: `home/` (HOME) and `proj/` (cwd). Disposed after
  each test (retry deletes on Windows).
- `Sandbox.SeedHome(IndexFixture index, bool withToolUrls = true)`:
  - `home/.ssotme/tool_urls.json` = `{ "cli-cloud-bridge": "http://127.0.0.1:{port}/bridge/" }`
  - `home/.ssotme/remote_tools/ssotme-tools.json` = the fixture rendered with the mock port,
    `home/.ssotme/remote_tools/cli_version` = the legacy version string (read from `package.json`),
    `home/.ssotme/remote_tools/effortless.json` = the minimal `remote_tools` project
    (see `ConfigFiles.remote-tools-project`).
  - Variants: `SeedEmptyHome()` (bootstrap test), `SeedEmptyIndex()`.
- `Sandbox.SeedProject(string fixtureName)` copies `tests/fixtures/projects/<name>/` into `proj/`.
- `Sandbox.WriteFile`, `ReadJson`, `ProjectFile` (parsed `effortless.json`) helpers.
- Never touch the real `~/.ssotme`. Add a guard test that fails if `HOME` equals the real home.

## 4. `MockToolServer`

`HttpListener` on `http://127.0.0.1:{free port}/`. Routes:

| Route | Behavior |
|---|---|
| `POST /bridge/` | Emulates `cli-cloud-bridge`. Parse the request JSON case-insensitively; read `cliParams` for `mode=` (default `list`), `cli_version=`, `email=`, `code=`, `jwt=`. `list` → respond with a FileSet containing **`ssotme-tools.json`** = `server.IndexJson` (configurable per test, with `cliUpdateAvailable` / `latestBridgeVersion` injectable). `auth`/`verify`/`refresh`/`viewPlan` → FileSet containing **`auth-result.json`** from `server.AuthResponses` (dictionary by mode). Records every request. |
| `POST /tools/{name}/{version}/` | Runs `server.Behaviors[name]` (a queue of scripted responses, defaulting to `Echo`). Records the request: raw JSON, parsed `cliParams`, `cliInput`, `cliOutput`, `cliJwt`, `cliAccount`, `cliWaitTimeout`, `transpiler.name`, and the **decoded input FileSet** (base64 → gunzip → XML → files). |
| `GET /tools/{name}/{version}/task/{id}` | Async task state machine per behavior (`pendingPolls` then completed/failed/404). |
| `GET /tools/{name}/{version}/` | `{"status":"healthy"}` (mirrors real tools; not required by the CLI). |

Scripted behaviors (compose per test):
- `Echo` — returns `Output.txt` = uppercase of the first input file (mirrors `to-uppercase`).
- `Files(params (path, kind, contents, overwrite, skipClean)[])` — kinds: `FileContents`,
  `ZippedTextFileContents`, `ZippedFileContents`, `BinaryFileContents`, `ZippedBinaryFileContents`, `NoContent`.
- `Logs(params (level, text)[])` added to any response.
- `Exception(message, inner?, stackTrace?)`.
- `Status(int code, string body, int times)` then fall through to the next behavior.
- `Text(string body)` (non-JSON 200).
- `Delay(ms)`.
- `Async(pendingPolls, thenBehavior | failed | notFound)`.

Response shape (mirror `FileSetService.ToSSOTMEPayloadOutputFileSet` + `EffortlessHelper`): JSON with
`TranspileRequest: { ZippedOutputFileSet: base64(gzip(FileSetXml)) }`, `Transpiler: { Name }`, `Logs: [...]`,
`SSoTmeProject: null`, `Exception: null | { Message, StackTrace, InnerException }`, and for async
`TaskId`, `TaskStatus`. PascalCase, like the real tools. The FileSet XML must use the exact element names
in `WirePayloadFields.fs-xml` (`XmlSerializer` layout: `<FileSet><FileSetFiles><FileSetFile>…`); gzip with
`GZipStream`; the CLI also accepts `ZippedFileContents` as a synonym for `ZippedTextFileContents` on the
way in, but produce what real tools produce (`FileContents` for text, `ZippedBinaryFileContents` for binary).

## 5. Index fixtures

`tests/fixtures/index/base.json` — `transpilerVersions` with:
- `effortless/common/to-uppercase`: `v2026.01.01.0001` (isHeadVersion true) and `v2025.12.31.2359`
  (false); `urls.post = "http://127.0.0.1:{port}/tools/to-uppercase/{version}/"`.
- `effortless/common/echo`: one head version.
- `effortless/common/to-lowercase`: **absent** here, present in `refreshed.json` (for `res-refresh-on-miss`).
- `ambiguous.json`: adds `acme/x/echo` and `effortless/y/echo`.
- `no-head.json`: all `isHeadVersion:false`.
Keep the metadata objects shaped like the real index (see `~/.ssotme/remote_tools/ssotme-tools.json` on
the owner's machine; the `metaData.isHeadVersion` and `urls.post` keys are the only ones the CLI reads).

## 6. Goldens and the legacy/rebuild switch

- `Golden.Normalize(text, sandbox)`: replace the sandbox project root with `<ROOT>`, home with `<HOME>`,
  the mock base URL with `<MOCK>`, GUIDs with `<GUID>`, ISO timestamps with `<TIME>`, the CLI version with
  `<VER>`, `\r\n` → `\n`, trailing whitespace trimmed.
- `Golden.Assert(testCaseId, normalizedOutput)`: compares to `Goldens/<id>.txt`; with
  `EFFORTLESS_RECORD_GOLDENS=1` it (re)writes the file instead. Goldens are **recorded from the legacy
  binary in this step** and committed.
- `Behavior.IsLegacy` = `EFFORTLESS_CLI_MODE == "legacy"`. Use it only in tests whose `TestCases.Description`
  or option `Disposition` says `keep-modified` (shim argv/exit code, child-process spawn, the proxy fallback
  message, the RabbitMQ wording in the refresh warning). Everything else asserts identical output.
- Prefer `Contains`-style assertions on the `UserMessages` templates for robustness; use full goldens for
  the structured outputs (`describe`, `listVersions`, `listUrls`, `info`, build summary, errors.json).

## 7. Implementation order

1. Harness + `meta-version-flag` green. Commit.
2. `MockToolServer` + `tx-request-shape` + `tx-output-rules` green (these prove the wire codec). Commit.
3. `e2e-project`, `e2e-install`, `e2e-build`, `e2e-clean` suites. Commit per suite.
4. `e2e-resolution`, `e2e-tool-urls`, `e2e-transpile` (remaining), `e2e-auth`, `e2e-shim`.
5. Mark `Slow` tests with `[Trait("Slow","true")]`; interactive tests feed stdin through `CliUnderTest.Run`.
6. Add `.github/workflows/ci.yml` (first version): ubuntu + macos + windows; `scripts/test-legacy.sh`;
   a second job `slow` on ubuntu only. This is the earliest CI the repo gets; keep it.
7. Update the rulebook: `TestCases.Status` → `implemented` / `legacy-green` for every case you ran;
   `RefactorSteps.step-01.Status` → `done`. Commit `step-01: characterization suite green on legacy`.

## 8. Things you will run into (all documented in the rulebook)

- `effortless -describe` outside a project prints the `no-project` message via `P11`, not the silent
  `ProjectNotConfiguredException`; the silent path is reachable via `describe` **inside** a directory whose
  project file is invalid after load — record what the legacy binary does and pin it (`proj-no-project-silent`).
- The legacy `tx-tool-not-found` case tries `https://proxy.effortlessapi.com/` first (DNS failure retries,
  ~60 s). Give it `-w 8000` and mark it `Slow`; assert the final "does not exist" block in both modes.
- `install` captures `Environment.CommandLine`; when the harness runs `dotnet <dll> args`, the legacy code
  strips `/ssotme.ost.cli.dll` — that is why `inst-prefix-strip-aliases` matters.
- Bridge refresh is triggered by "tool missing from index", not by CLI version; seed `cli_version` to avoid
  surprises and assert the banner `WHY:` text.
- Windows: `HttpListener` needs `http://127.0.0.1:{port}/` (not `+`) to avoid URL ACL prompts.

**Done when:** `scripts/test-legacy.sh` is green locally on macOS (and on the CI matrix), every P0/P1
`e2e-*` `TestCaseId` except `planned-step-03a` rows exists as an xunit `DisplayName`, goldens are
committed, and the implemented rulebook statuses are updated.
