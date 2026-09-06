# Step 12 — Local transpiler host: the CLI as a transpiler target

**Goal:** a project can drop a tool at `./effortless-tools/<tool-name>/` and reference `<tool-name>` in
`effortless.json` exactly like a catalog tool. The CLI provides the HTTP host, the wire contract, the
fileset plumbing, and the lifecycle. Tool authors only write the tool.

## Why, and what it replaces

`../effortless-rulebooks/rulebook-examples/legacy-runner/ssotme-proxy/server.py` is the pattern in the
field: a Python server on `localhost:4242` where each route is a local transpiler, installed into projects
as `http://localhost:4242/<tool>`. It works, but it never consumes the request payload; it recovers the
CLI's working directory by looking up the client socket's PID with `lsof` and reading that process's cwd,
and it returns an empty fileset so the CLI writes nothing. Every project that wants local tools has had
to rebuild that scaffolding. This step makes it CLI infrastructure and makes it use the real contract.

`-execute` (LocalCommand steps) stays as-is and coexists (D26). Local tools are the fileset-aware,
ledgered option for when a step should behave exactly like a published tool.

## Design

**Discovery.** From the project root: `effortless-tools/<tool-name>/` with a `tool.json`
(`{ "name", "runtime": "dotnet" | "node" | "script", "entry", "description", "tags" }`); when `tool.json`
is absent, a `*.csproj` means `dotnet`, a `package.json` means `node`, and a single executable
`transpiler.*` means `script`. Tool names must be valid catalog-style lower-hyphen names. Nested effortless projects have their
own `effortless-tools/`; a parent's tools are not inherited (same rule as project settings).

**Resolution.** New `R12-local-tool` in `ToolResolutionRules`, evaluated after R1/R2 (explicit URL) and
before R4 (`tool_urls.json`) and R3 (catalog): if `<name>` matches a local tool in the current project,
resolve to `http://127.0.0.1:<port>/<name>`. A `-setUrl` override still wins over it, so a local tool can
be pointed elsewhere for debugging. Local tools are not catalog-versioned: no pin, no `[latest]`, the
`.zfs` key is `local/<name>`.

**Wire contract.** The host speaks the exact contract cloud tools speak: `TranspilePayload` in
(`CLIInput`, `CLIInputFileSetXml` / `TranspileRequest.ZippedInputFileSet`, `cliParams`, `cliAccount`,
`cliWaitTimeout`), and the response with `Transpiler` + `TranspileRequest.ZippedOutputFileSet` +
`OutputDisposition`. Nothing about the CLI side changes; `SaveFileSet`, ledgers, `clean`, `-debug` all
behave as for a remote tool. That is the whole point.

**Host.** `effortless serve [-port N]`:

- Unzips the input fileset into a per-request temp dir, invokes the tool's entry as a child process with
  a small, documented contract (script shape): env `EFFORTLESS_INPUT_DIR`, `EFFORTLESS_OUTPUT_DIR`,
  `EFFORTLESS_OUTPUT_NAME`, `EFFORTLESS_PARAMS` (JSON of `cliParams`), `EFFORTLESS_TOOL_NAME`;
  stdout/stderr become the tool log.
  Non-zero exit = tool failure with the log in the response. After exit, the output dir is zipped as the
  output fileset.
- Resident mode: `effortless serve` stays up, watches `effortless-tools/`, reloads on change, prints the
  URL per tool, `GET /` lists tools (like the proxy did).
- Ephemeral mode: `effortless build` that resolves any R12 tool checks `<root>/.effortless/serve.json`
  (port + pid); if no live host, starts one on an ephemeral port for the duration of the build and stops
  it afterwards. Output identical either way.

**Runtime — decided (D30): native .NET, plus a node/express fileset handler.** The host is
in-process .NET (Kestrel) inside the CLI binary; the fileset zip/unzip, payload types and ledger code
already live in `Effortless.Cli.Core`. Three tool shapes, declared by `tool.json.runtime`:

- `dotnet`: the tool is a small web app built on the same tool-side `CLIClassLibrary` contract that every
  published cloud tool uses. A local tool folder is literally a cloud tool workload run locally. The host
  starts it (`dotnet run --project`) on an ephemeral port and proxies the payload to it. Because it is the
  same shape, `publish-tool.sh` can later publish it unchanged.
- `node`: ship a node module (`lib/fileset-handler.mjs` inside the `@effortlessapi/cli` package,
  publishable separately later) implementing the same fileset request/response logic. A node tool is an
  express app with one route that calls the author's `transpile({ inputFiles, params, outputName })` and
  returns output files; the handler does the zip/unzip and the payload shape. The host starts it and
  proxies, exactly as for `dotnet`.
- `script` (`.py`, `.sh`, anything executable): the host does the fileset work itself and hands the tool
  a directory via the env contract above (`EFFORTLESS_INPUT_DIR`, `EFFORTLESS_OUTPUT_DIR`,
  `EFFORTLESS_OUTPUT_NAME`, `EFFORTLESS_PARAMS`, `EFFORTLESS_TOOL_NAME`). Lightweight path for one-off tools.

`EFFORTLESS_OUTPUT_NAME` is the `-output` value; the tool decides whether it is a file or a directory
(D21). Missing runtime = clear error naming the runtime and the tool.

**Out of scope.** Publishing a local tool to the catalog (that is the transpiler-server's job; link to the
publish flow), and fronting cloud tools through the local host for offline use.

## Rulebook changes

`CliOptions`: `serve` (primary), `port` (modifier of `serve`). `ToolResolutionRules`: R12.
`ConfigFiles`: `effortless-tools/<name>/tool.json`, `.effortless/serve.json`. `LifecycleStates`: the
ephemeral host start/stop around a build. `UserMessages`: host started/stopped, tool list, runtime
missing, tool failed. `EnvVariables`: the four `EFFORTLESS_*` tool-contract variables.

## Tests

Fixture project with one tool per shape: `effortless-tools/to-upper-dotnet/` (csproj),
`effortless-tools/to-upper-node/` (package.json + fileset handler), `effortless-tools/echo-params/transpiler.sh`. E2E: `local-tool-resolves-before-catalog`, `local-tool-build-writes-output`,
`local-tool-ledger-and-clean`, `local-tool-ephemeral-host-lifecycle`, `local-tool-resident-serve-reuse`,
`local-tool-params-passthrough`, `local-tool-nonzero-exit-fails-step`, `local-tool-missing-runtime`, `local-tool-node-handler-roundtrip`, `local-tool-dotnet-same-shape-as-cloud`,
`local-tool-seturl-override-wins`, `serve-lists-tools`, `local-tool-not-inherited-by-child-project`.
Unit: discovery, name validation, runtime mapping.

## Done when

The fixture tools build, clean, and re-build byte-identically to an equivalent remote tool; `serve` runs
resident and ephemeral; the legacy-runner README points here as the successor to `ssotme-proxy`; tests
green; `RefactorSteps.step-12.Status` → `done`.

## As built (2026-09-05)

Everything above shipped with these concrete decisions, each recorded in the rulebook:

- **Host runtime.** `System.Net.HttpListener`, not Kestrel. Kestrel would add the ASP.NET Core shared
  framework to the CLI's install requirements and break the two-dependency cut; HttpListener is in the
  base framework and is the same server the E2E mock tool already uses. D30's "native .NET host in the
  CLI" holds.
- **Ledger key** is `local-<name>` (the step-15 form, not `local/<name>`): a slash would nest a
  directory under the ledger, and the key must not contain the host port, which changes per run.
  `TranspileClient`, `CleanRunner` (clean and purge) and `Uninstall` all derive it from the discovered
  catalog through `CliInvocation.LedgerKey`. Local runs never write `LastUrl`/`LastVersionUsed`.
- **Precedence.** `tool_urls.json` (R4) beats R12, so `-setToolUrl <name>=…` redirects a local tool.
  R12 beats the catalog (R3), and a local name never triggers a catalog refresh
  (`RequiresFreshCatalogBeforeResolution`) nor the project currentness gate (`ProjectToolFreshness`).
- **Proxied shapes (dotnet/node)** get `PORT` (what `CLIClassLibrary.StartToolListener` reads) plus
  `EFFORTLESS_TOOL_PORT` and `EFFORTLESS_TOOL_NAME`; the host waits for the port to accept connections
  (3 min budget, `dotnet run` builds first) and forwards `POST /` and `GET /task/<id>` verbatim.
- **Node handler** is `lib/fileset-handler.mjs`: zero dependencies (node:http + node:zlib), embedded in
  the Core assembly and extracted to `<root>/.effortless/local-tools/`, handed to the tool as
  `EFFORTLESS_FILESET_HANDLER`. It exposes `createRequestListener` (a plain `(req, res)` handler, so it
  mounts in express) and `serveTool`. No `npm install` in the tool folder is required.
- **Script outputs** obey the protocol's overwrite rules untouched. A script declares per-file modes in
  `effortless-overwrite-modes.json` at the root of `EFFORTLESS_OUTPUT_DIR` (`{ "<path or glob>": "Always"
  | "Never" }`, the exact `OverwriteMode` values; never shipped); an undeclared file carries no node and is
  written once, exactly like any other tool's undeclared file. Text XML can carry verbatim goes as
  `FileContents`, anything else as `ZippedBinaryFileContents`. (An earlier draft stamped every script file
  `AlwaysOverwrite`; that was a defect against the FileSet overwrite invariant and was removed the same day.)
- **Ephemeral host** lives in `LocalToolResolver` on the dispatcher, one per project root per CLI
  invocation, disposed in `CommandDispatcher.Run`'s `finally`. It never writes `serve.json`.
- **Resident host** (`serve`) reloads discovery on `effortless-tools/` changes (500 ms debounce),
  handles SIGINT/SIGTERM, and removes `serve.json` on exit. `EFFORTLESS_SERVE_EXIT_AFTER_MS` is the
  test seam. Stale `serve.json` (dead pid or closed port) is ignored.
- **Fixture** `tests/fixtures/projects/project-local-tools/` has all three shapes plus `fail-tool`;
  the dotnet fixture is dependency-free (HttpListener) so it restores offline.
