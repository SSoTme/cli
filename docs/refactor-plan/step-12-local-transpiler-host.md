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

`-execute` (LocalCommand steps) stays as-is. Local tools are the fileset-aware, ledgered upgrade of it.

## Design

**Discovery.** From the project root: `effortless-tools/<tool-name>/` with either a `tool.json`
(`{ "name", "runtime", "entry", "description", "tags" }`) or the convention `transpiler.<ext>` as the
entry. Tool names must be valid catalog-style lower-hyphen names. Nested effortless projects have their
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
  a small, documented contract: env `EFFORTLESS_INPUT_DIR`, `EFFORTLESS_OUTPUT_DIR`,
  `EFFORTLESS_PARAMS` (JSON of `cliParams`), `EFFORTLESS_TOOL_NAME`; stdout/stderr become the tool log.
  Non-zero exit = tool failure with the log in the response. After exit, the output dir is zipped as the
  output fileset.
- Runtimes by extension or `tool.json.runtime`: `.mjs`/`.js` → `node`, `.ts`/`.tsx` → `node` with
  type-stripping (or `tsx` when present), `.py` → `python3`, `.sh` → `bash`, `.cs`/`.csproj` →
  `dotnet run`. Missing runtime = clear error naming the runtime and the tool.
- Resident mode: `effortless serve` stays up, watches `effortless-tools/`, reloads on change, prints the
  URL per tool, `GET /` lists tools (like the proxy did).
- Ephemeral mode: `effortless build` that resolves any R12 tool checks `<root>/.effortless/serve.json`
  (port + pid); if no live host, starts one on an ephemeral port for the duration of the build and stops
  it afterwards. Output identical either way.

**Runtime of the host itself — owner decision needed.** Two options:

- **A (recommended):** in-process .NET (Kestrel) inside the CLI binary. The fileset zip/unzip, payload
  types and ledger code already live in `Effortless.Cli.Core`; there is nothing to re-implement, no node
  dependency for the host, one binary. Tool entries can still be node/python/sh/dotnet.
- **B:** a node/express host shipped in the npm package. Matches the "local node express service" idea
  literally, but duplicates the wire contract in JS and makes the MSI/PKG installs depend on node.

**Out of scope.** Publishing a local tool to the catalog (that is the transpiler-server's job; link to the
publish flow), and fronting cloud tools through the local host for offline use.

## Rulebook changes

`CliOptions`: `serve` (primary), `port` (modifier of `serve`). `ToolResolutionRules`: R12.
`ConfigFiles`: `effortless-tools/<name>/tool.json`, `.effortless/serve.json`. `LifecycleStates`: the
ephemeral host start/stop around a build. `UserMessages`: host started/stopped, tool list, runtime
missing, tool failed. `EnvVariables`: the four `EFFORTLESS_*` tool-contract variables.

## Tests

Fixture project with `effortless-tools/to-upper/transpiler.mjs` and `effortless-tools/echo-params/
transpiler.sh`. E2E: `local-tool-resolves-before-catalog`, `local-tool-build-writes-output`,
`local-tool-ledger-and-clean`, `local-tool-ephemeral-host-lifecycle`, `local-tool-resident-serve-reuse`,
`local-tool-params-passthrough`, `local-tool-nonzero-exit-fails-step`, `local-tool-missing-runtime`,
`local-tool-seturl-override-wins`, `serve-lists-tools`, `local-tool-not-inherited-by-child-project`.
Unit: discovery, name validation, runtime mapping.

## Done when

The fixture tools build, clean, and re-build byte-identically to an equivalent remote tool; `serve` runs
resident and ephemeral; the legacy-runner README points here as the successor to `ssotme-proxy`; tests
green; `RefactorSteps.step-12.Status` → `done`.
