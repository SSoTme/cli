# Upgrading from the SSoT.me CLI

The Effortless CLI is the successor to the SSoT.me CLI (`ssotme`). The last
release of that line is `2026-06-09.06.13`, commit
`a8f0f320f4417c58fa931cc4bc8164f79cbbbd97` (tag `legacy-final`, branch
`legacy/main`). This page is the only place the old line is described.

## What happens automatically

| Old | New | When |
|---|---|---|
| `~/.ssotme/` | `~/.effortless/` | First run: copied, never moved; `~/.ssotme/MIGRATED-TO-EFFORTLESS` records it. `ssotme.key` becomes `effortless.key`, `remote_tools/ssotme-tools.json` becomes `remote_tools/effortless-tools.json`. |
| `<project>/ssotme.json`, `aicapture.json`, `SSoTmeProject.json` | `effortless.json` | On project load, renamed in place. |
| `<project>/ssotme.env` | `effortless.env` | On project load, when `effortless.env` is absent. |
| `<project>/.ssotme/` (ledger) | `<project>/.effortless/` | On project load. `init` writes both ignore lines so old clones stay clean. |
| `ssotme-seed.json` | `effortless-seed.json` | Both are read. |
| `SSOTME_CHILD_PROCESS` | `EFFORTLESS_CHILD_PROCESS` | Set by the CLI around child builds. |

The `ssotme`, `aicapture`, and `aic` commands remain as aliases of `effortless`.
The `ssotme://` protocol scheme is unchanged. Command lines in existing
`effortless.json` files that start with `ssotme ` still work.

## What changed on purpose

- **Transport.** Tools are reached only over HTTPS with the JSON transpile
  contract. The message-queue transport (RabbitMQ) and every option, file, and
  fallback that depended on it are gone; a tool that cannot be reached is a
  hard error, never a silent fallback to a stale catalog.
- **Package.** `npm install -g @effortlessapi/cli` (the unscoped `effortless`
  package is unrelated software). Installers are `Effortless-Installer-*`.
- **Catalog.** The tool catalog refreshes itself after 24 hours and whenever a
  catalog-resolved tool is unreachable; `listTools` and `searchTools` filter by
  text, category, account, and date. Project steps are kept current against the
  catalog automatically; `-pin` is honoured while the pinned version exists.
- **Help.** `-help` is tiered by category; `-help <topic>` explains one option.
- **Local tools.** `effortless-tools/<name>/` in a project is hosted by the CLI
  itself (`serve`), replacing per-project proxy servers.
- **Seeds.** Discovery spans an ordered list of GitHub accounts
  (`listSeedSources`, `addSeedSource`, `removeSeedSource`); the built-in
  defaults are `ssotme` and `effortlessapi`.
- **Authentication.** `login`, `projectLogin`, `plan`, and `logout` call the
  published `effortless-auth` tool; the service is a preview that enforces
  nothing yet and says so.

## Removed options

| Removed | Use instead |
|---|---|
| `-legacy`, `-discuss`, `-latest`, `-checkResults`, `-createDocs`, `-localGuide`, `-copilot`, `-dryRun`, `-authenticate` (key file), `-updateUrls` | Nothing; these belonged to the message-queue transport or to the retired self-documentation pipeline. |
| `-viewUrl`, `-setUrl`, `-removeUrl`, `-listUrls` | `-viewToolUrl`, `-setToolUrl`, `-removeToolUrl`, `-listToolUrls` (the old spellings still work as aliases). |
| Airtable schema guessing for seed values | Explicit `$key$` replacements in `effortless-seed.json`. |

Run `effortless -help all` for the complete option list, or read
[cli-reference.md](cli-reference.md).
