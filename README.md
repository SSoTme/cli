# Effortless CLI

`effortless` is the command-line client for EffortlessAPI transpilers. It reads an
`effortless.json` project, resolves tools from the REST catalog, sends file sets to
those tools over HTTP, and writes or cleans the generated outputs.

The same CLI is installed under four compatible command names: `effortless`,
`ssotme`, `aicapture`, and `aic`.

## Install

### npm from source

.NET 8 and Node.js are required:

```bash
git clone https://github.com/EffortlessAPI/cli.git
cd cli
npm install -g .
effortless -version
```

The npm shim builds `Effortless.Cli.sln` in Release mode when the compiled CLI is
missing or the package version changed.

### Windows MSI and macOS PKG

Download the appropriate MSI or PKG from the repository's GitHub Releases page.
Both installers place all four command aliases on `PATH`.

To upgrade an existing installation from the command line:

```bash
effortless -upgradeCli
```

## Quick start

Initialize a project in the current directory:

```bash
effortless -init
```

Install a transpiler invocation into `effortless.json`:

```bash
effortless rulebook-to-rulespeak -install \
  -i effortless-rulebook/effortless-rulebook.json
```

Run all enabled project steps:

```bash
effortless build
```

Remove files recorded in the generated-file ledgers:

```bash
effortless clean
```

## Seeds

An Effortless seed is a public GitHub repository with `effortless.json` at its
root. List seeds from an account and clone one without automatically executing
downloaded code:

```bash
effortless listSeeds ssotme
effortless cloneSeed seed-name my-project
cd my-project
effortless build
```

Set `EFFORTLESS_SEED_GITHUB_ACCOUNT` to change the default account. Projects
containing `ssotme-seed.json` retain the `$key$` content and filename replacement
contract when loaded.

## Build on a cloud trigger

Watch the live Airtable trigger bridge and rebuild after changes have been quiet
for ten seconds:

```bash
effortless build -buildOnTrigger <baseId>
```

The watcher polls every three seconds. Transport, HTTP, or malformed-payload
failures stop the command instead of being treated as an unchanged base.

Use `effortless -help` for command-line help. The generated command reference is
at [docs/cli-reference.md](docs/cli-reference.md), and the REST-only rebuild
history is under [docs/refactor-plan/](docs/refactor-plan/).

## Surviving a broken transpiler: `-continueOnError`

By default, `effortless build` stops at the first step that fails. That is the
right default for CI and for a one-shot build you are watching — but it is the
wrong default for any long-running host that builds a whole pipeline and then
runs the result, because one non-load-bearing step (an export, a docs
generator) takes down every load-bearing step with it.

```
effortless build -continueOnError
```

Aliases: `-coe`, `-ignoreErrors`, `-ignoreError`. Defaults to **off**.

With the flag set:

- **Every remaining step still runs.** A step that fails — whether it returns a
  non-zero result *or throws* — is recorded and skipped, and the build moves on.
  (The older `-ignoreErrors` flag only ever handled the non-zero-return case; a
  throwing step still killed the whole build. That gap is fixed.)
- **The build exits 0.** The run completed; some steps did not.
- **Failures are written to `errors.json` in the project root**, so nothing is
  lost when the build no longer stops to show you. The file is deleted on a
  clean build, so its presence always describes the *most recent* build:

```jsonc
{
  "schema": "effortless-build-errors/v1",
  "generatedAt": "…", "projectRoot": "…", "buildCommand": "build",
  "continueOnError": true,
  "totalSteps": 7, "succeededSteps": 6, "failedSteps": 1, "skippedSteps": 0,
  "failedStepNames": ["rulebooktoxlsx"],
  "steps": [ /* every step, in order, with status — the lightweight index */ ],
  "errors": [
    {
      "name": "rulebooktoxlsx",
      "relativePath": "/xlsx",
      "commandLine": "rulebook-to-xlsx -i ../effortless-rulebook/effortless-rulebook.json",
      "status": "failed",
      "exitCode": -1,
      "message": "…",
      "resolvedVersion": "…", "resolvedUrl": "…",
      "transpilerException": { "type": "…", "message": "…", "stackTrace": "…", "inner": { } },
      "cliException":        { "type": "…", "message": "…", "stackTrace": "…", "inner": { } }
    }
  ]
}
```

`transpilerException` is what the tool itself reported back over the wire;
`cliException` is anything the CLI threw while running that step. Both walk the
full `InnerException` chain — the real cause is often two or three levels below
the message that reaches the console.

A summary is also printed at the end of the build naming each failed step, its
command line, and the path to `errors.json`.

## Contributing

Create a branch, open a pull request, and keep `dotnet test Effortless.Cli.sln`
green. Changes are squash-merged. Maintainers release through
`scripts/release.sh`; do not hand-roll version or publishing steps.

## Legacy

The last commit of the original SSoTme/RabbitMQ-era CLI is a8f0f320f4417c58fa931cc4bc8164f79cbbbd97 (tag legacy-final, branch legacy/main).

## License

See [LICENSE](LICENSE).
