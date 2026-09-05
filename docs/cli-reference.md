<!-- Generated from effortless-rulebook.json. Do not edit. -->
# Effortless CLI reference

## Help syntax

```text
Syntax: effortless [account/]transpiler [Options]
```

## CLI meta

Options about the CLI itself: help, version, info, debug, self-upgrade.

### `-help`

- Aliases: `h`
- Bareword forms: `help`
- Value type: `bool`
- Help text: Show help about how to use the Effortless CLI
- Description: Print usage.

### `-debug`

- Aliases: None
- Bareword forms: None
- Value type: `bool`
- Help text: Show debug output
- Description: Verbose diagnostics.

### `-info`

- Aliases: None
- Bareword forms: `info`
- Value type: `bool`
- Help text: Show configured settings
- Description: Show CLI/user configuration.

### `-version`

- Aliases: `v`
- Bareword forms: `version`, `v`
- Value type: `bool`
- Help text: Show CLI version
- Description: Print the version.

### `-upgradeCli`

- Aliases: `uc`, `update`
- Bareword forms: None
- Value type: `bool`
- Help text: Upgrade the effortless CLI to the latest version
- Description: Self-update from GitHub releases.

## Project file

Creating, describing and configuring the effortless.json project.

### `-init`

- Aliases: None
- Bareword forms: `init`
- Value type: `bool`
- Help text: Initialize the current folder as the root of an Effortless project. An Optional parameter of force will create a sub-project.
- Description: Create effortless.json, .gitignore, effortless.env and an empty rulebook in cwd.

### `-describe`

- Aliases: `d`
- Bareword forms: `list`, `describe`
- Value type: `bool`
- Help text: Describe the transpilers in the current folder and its children
- Description: Print project summary for steps at or below cwd (v2: downstream, like build; was local-only in v1).

### `-describeAll`

- Aliases: `da`
- Bareword forms: `da`, `describeall`
- Value type: `bool`
- Help text: Describe all of the transpiler in the project
- Description: Print the whole project.

### `-listSettings`

- Aliases: `ls`
- Bareword forms: None
- Value type: `bool`
- Help text: List of project settings
- Description: Print ProjectSettings.

### `-addSetting`

- Aliases: `as`
- Bareword forms: None
- Value type: `List<string>`
- Help text: Adds a setting to the Effortless Project
- Description: Add/replace a ProjectSetting.

### `-removeSetting`

- Aliases: `rs`
- Bareword forms: None
- Value type: `List<string>`
- Help text: Removes a setting from the Effortless Project
- Description: Remove a ProjectSetting.

### `-projectName`

- Aliases: `name`
- Bareword forms: None
- Value type: `string`
- Help text: Name of the project (optional parameter to the init command)
- Description: Project name for -init.

### `-describeLocal`

- Aliases: `dl`
- Bareword forms: `describelocal`
- Value type: `bool`
- Help text: Describe only the transpilers installed in the current folder
- Description: Print project summary for steps registered exactly at cwd.

### `-describeWithSubprojects`

- Aliases: `dws`
- Bareword forms: `describewithsubprojects`
- Value type: `bool`
- Help text: Describe the whole project, including nested effortless projects normally excluded
- Description: Print the whole project, including nested effortless projects.

## Install / uninstall tools

Registering transpiler steps in effortless.json.

### `-install`

- Aliases: None
- Bareword forms: `install`
- Value type: `bool`
- Help text: Saves the current command into the Effortless Project file
- Description: Register a transpiler step in effortless.json (and run it once).

### `-uninstall`

- Aliases: None
- Bareword forms: `uninstall`
- Value type: `bool`
- Help text: Removes the current command from the Effortless Project file
- Description: Remove a registered step.

### `-execute`

- Aliases: `exec`
- Bareword forms: None
- Value type: `string`
- Help text: Executes the given command as a ProcessInfo.Start
- Description: Run a local command as a (registered) step.

### `-transpilerGroup`

- Aliases: `tg`
- Bareword forms: None
- Value type: `string`
- Help text: Name of a group to put a transpiler in within a specific folder
- Description: Group tag for a step.

### `-disable`

- Aliases: None
- Bareword forms: `disable`
- Value type: `bool`
- Help text: Marks the current command's step as disabled in the effortless.json Project file
- Description: Mark a registered step disabled (skipped by build).

### `-enable`

- Aliases: None
- Bareword forms: `enable`
- Value type: `bool`
- Help text: Marks the current command's step as enabled in the effortless.json Project file
- Description: Mark a registered step enabled (included in build).

## Build

Running registered transpiler steps.

### `-build`

- Aliases: `b`, `replay`, `rebuild`, `pull`
- Bareword forms: `build`, `rebuild`, `pull`
- Value type: `bool`
- Help text: Build any transpilers in the current folder (or children).
- Description: Run every enabled step at or below cwd.

### `-buildAll`

- Aliases: `ba`, `replayall`, `rebuildAll`, `pullAll`
- Bareword forms: `buildall`, `rebuildall`, `pullAll`
- Value type: `bool`
- Help text: Builds all transpilers in the project
- Description: Run every step in the whole project, excluding nested effortless projects.

### `-buildLocal`

- Aliases: `bl`, `replaylocal`, `rebuildLocal`, `pullLocal`
- Bareword forms: `buildlocal`
- Value type: `bool`
- Help text: Build only the transpilers installed in the current folder
- Description: Run only steps whose RelativePath equals cwd.

### `-buildOnTrigger`

- Aliases: `bot`
- Bareword forms: None
- Value type: `string`
- Help text: Builds whenever a trigger is invoked (see readme for URL)
- Description: Watch an Airtable base and rebuild on change.

### `-includeDisabled`

- Aliases: `id`
- Bareword forms: None
- Value type: `bool`
- Help text: Include disabled tools in the build
- Description: Build disabled steps as well.

### `-continueOnError`

- Aliases: `coe`, `ignoreErrors`, `ignoreError`
- Bareword forms: None
- Value type: `bool`
- Help text: Don't let one failing step stop the build: run every remaining transpiler, write the full exception detail of anything that failed to errors.json in the project root, and still exit 0. Defaults to off.
- Description: Keep building after a failed step.

### `-buildWithSubprojects`

- Aliases: `bws`
- Bareword forms: `buildwithsubprojects`
- Value type: `bool`
- Help text: Build the whole project, including nested effortless projects normally excluded
- Description: Run every step in the whole project, including nested effortless projects.

## Clean

Removing generated output using the .zfs ledgers.

### `-clean`

- Aliases: `c`
- Bareword forms: `clean`
- Value type: `bool`
- Help text: Clean all transpilers installed in and downstream of this folder
- Description: Delete generated files recorded in .zfs ledgers.

### `-cleanAll`

- Aliases: `ca`
- Bareword forms: `cleanall`
- Value type: `bool`
- Help text: Clean all project transpilers
- Description: Clean the whole project, excluding nested effortless projects.

### `-cleanLocal`

- Aliases: `cl`
- Bareword forms: `cleanlocal`
- Value type: `bool`
- Help text: Cleans only transpilers installed in the current folder
- Description: Clean only steps registered exactly at cwd.

### `-purge`

- Aliases: None
- Bareword forms: None
- Value type: `bool`
- Help text: When supplied with clean, runs clean on all orphaned transpilers
- Description: Also clean ledgers of steps that are no longer registered.

### `-preserveZFS`

- Aliases: `rz`
- Bareword forms: None
- Value type: `bool`
- Help text: Determines if the input should be preserved.
- Description: Keep .zfs ledgers when cleaning.

### `-cleanWithSubprojects`

- Aliases: `cws`
- Bareword forms: `cleanwithsubprojects`
- Value type: `bool`
- Help text: Clean the whole project, including nested effortless projects normally excluded
- Description: Clean the whole project, including nested effortless projects.

## Transpile inputs & outputs

Options that shape a single transpile request: inputs, output name, parameters, account credentials, timeout, target URL.

### `-input`

- Aliases: `i`
- Bareword forms: None
- Value type: `List<string>`
- Help text: Input filename or comma separated list of file names
- Description: Input file(s) sent to the tool.

### `-output`

- Aliases: `o`
- Bareword forms: None
- Value type: `string`
- Help text: Output filename
- Description: Requested output filename for the tool.

### `-skipClean`

- Aliases: `sc`
- Bareword forms: None
- Value type: `bool`
- Help text: Don't clean the output before cooking
- Description: Write outputs without first cleaning the previous run.

### `-account`

- Aliases: `a`
- Bareword forms: None
- Value type: `string`
- Help text: The account which the transpiler belongs to
- Description: Credential/account selector for a tool.

### `-parameters`

- Aliases: `p`
- Bareword forms: None
- Value type: `List<string>`
- Help text: A list of parameters
- Description: key=value parameters for the tool.

### `-waitTimeout`

- Aliases: `w`
- Bareword forms: None
- Value type: `int`
- Help text: The amount of time to wait for the command to continue
- Description: Timeout in milliseconds.

### `-targetUrl`

- Aliases: `g`
- Bareword forms: None
- Value type: `string`
- Help text: TargetUrl of the tool being invoked
- Description: POST directly to a tool URL.

## Tool resolution & versions

How a tool name becomes a URL: remote index, versions, pins, upgrades.

### `-listVersions`

- Aliases: `lv`, `list`, `l`
- Bareword forms: None
- Value type: `bool`
- Help text: List all available versions of the tool
- Description: List versions of a tool from the remote index.

### `-refreshTools`

- Aliases: `rt`
- Bareword forms: `refreshtools`
- Value type: `bool`
- Help text: Purge and re-fetch the remote tools index
- Description: Force a re-download of the tools index.

### `-listTools`

- Aliases: `tools`, `lt`
- Bareword forms: `listtools`, `tools`, `lt`
- Value type: `bool`
- Help text: List all available tools in the current remote tools catalog
- Description: List canonical remote tool names and their current head versions.

### `-searchTools`

- Aliases: `search`, `st`
- Bareword forms: `searchtools`, `search`
- Value type: `string`
- Help text: Search the current remote tools catalog by tool name
- Description: Find tools by canonical or short name without guessing an exact tool name.

### `-upgrade`

- Aliases: `up`
- Bareword forms: `upgrade`, `unpin`
- Value type: `bool`
- Help text: Update the pinned version of this tool to the current head version (does not run the tool)
- Description: Unpin a tool so it tracks HEAD.

### `-upgradeAll`

- Aliases: `ua`
- Bareword forms: `upgradeall`
- Value type: `bool`
- Help text: Upgrade all transpilers in the project to the latest version
- Description: Unpin every tool.

### `-pin`

- Aliases: None
- Bareword forms: `pin`
- Value type: `string`
- Help text: Pin this project's step to a specific catalog version or URL
- Description: Pin an installed step to a version or URL for this project.

## Tool URL overrides

The per-user ~/.effortless/tool_urls.json override table.

### `-listToolUrls`

- Aliases: `lu`, `listUrls`
- Bareword forms: `listtoolurls`, `listurls`, `lu`
- Value type: `bool`
- Help text: List all custom tool urls defined for this user
- Description: List URL overrides.

### `-viewToolUrl`

- Aliases: `vu`, `vt`, `viewUrl`
- Bareword forms: `viewtoolurl`, `viewurl`, `vu`, `vt`
- Value type: `string`
- Help text: View the url for the specified tool
- Description: Show one URL override.

### `-setToolUrl`

- Aliases: `su`, `setUrl`
- Bareword forms: `settoolurl`, `seturl`, `su`, `st`
- Value type: `string`
- Help text: Set a tool's URL to a custom endpoint for this user
- Description: Add/replace a URL override.

### `-removeToolUrl`

- Aliases: `ru`, `removeUrl`
- Bareword forms: `removetoolurl`, `removeurl`, `ru`, `rt`
- Value type: `string`
- Help text: Remove a custom tool URL from this user's config, setting it back to the default value.
- Description: Remove a URL override.

## Authentication & account

Magic-link login, project login, subscription, API keys.

### `-runAs`

- Aliases: `ra`
- Bareword forms: None
- Value type: `string`
- Help text: Run as this user (look for this user's key file)
- Description: Use an alternate key file.

### `-setAccountAPIKey`

- Aliases: `api`, `setAccountKey`
- Bareword forms: None
- Value type: `string`
- Help text: Add an account api key
- Description: Store an API key for -account.

### `-login`

- Aliases: `auth`, `authenticate`
- Bareword forms: `auth`, `login`, `authenticate`
- Value type: `bool`
- Help text: Authenticate with EffortlessAPI using a magic link (currently unavailable; reserved for future service enablement).
- Description: Email magic-link login.

### `-projectLogin`

- Aliases: `projectAuth`
- Bareword forms: `projectlogin`, `projectauth`
- Value type: `bool`
- Help text: Authenticate this project with EffortlessAPI (currently unavailable; reserved for future service enablement).
- Description: Project-scoped login.

### `-logout`

- Aliases: `signout`
- Bareword forms: `logout`, `signout`
- Value type: `bool`
- Help text: Logout of your cli user account
- Description: Clear the global token.

### `-plan`

- Aliases: `subscription`
- Bareword forms: `subscription`, `plan`
- Value type: `bool`
- Help text: View the authenticated EffortlessAPI subscription plan (currently unavailable).
- Description: Show the account plan.

## Seeds (legacy scaffolding)

GitHub-hosted Effortless seed repositories (root or child projects with a full Effortless stack and effortless.json at the root). Kept per owner decision D1; see the effortless-skills seed skill (step 14).

### `-listSeeds`

- Aliases: `lsd`
- Bareword forms: `listseeds`
- Value type: `bool`
- Help text: Lists public Effortless seed repositories
- Description: List public Effortless seed repositories.

### `-cloneSeed`

- Aliases: `cs`, `clone`
- Bareword forms: `cloneseed`, `clone`
- Value type: `bool`
- Help text: Clones a public Effortless seed repository
- Description: Clone a public Effortless seed repository.

## Exit codes

- `0` — **Command completed; transpile output written; management command finished.**: Includes -continueOnError builds that had failures (documented contract: still exit 0).
- `-1` — **A user-facing error was printed and the command did not complete.**: ProjectNotConfiguredException prints NOTHING before exiting -1 (legacy quirk, kept).
- `-1` — **Unexpected exception; message printed in red then Environment.Exit(-1).**: Stack trace is not printed unless the exception surfaced through the TRANSPILER ERROR box path.
- `-1` — **A build step failed without -continueOnError: TranspilerStepFailedException propagates out of DoRebuild to Main.**: errors.json is still written by BuildErrorLog.Finish in the finally block before the exit.

## Configuration files

- `<root>/effortless.json` — The project: Name, SSoTmeProjectId, ProjectSettings, ProjectTranspilers (see ProjectFileFields). Indented JSON + trailing newline; custom non-managed properties on transpiler entries are preserved across saves (matched by Name+RelativePath+TranspilerGroup).
- `<root>/ssotme.json` — Legacy project file name. Also accepted as-is if the rename does not happen (e.g. effortless.json exists but is invalid).
- `<root>/aicapture.json and <root>/SSoTmeProject.json` — Older legacy project file names. Not renamed; loaded in place and saved back under the SAME name (GetProjectFI chooses the first valid candidate).
- `<root>/effortless.env` — Project-scoped credentials: EFFORTLESS_JWT and {ACCOUNT}_{PAT|API_KEY|APIKEY|KEY|BASEID|BASE_ID}. ssotme.env is auto-renamed to effortless.env when the latter is absent. Comment lines start with #; surrounding quotes are stripped; keys are case-insensitive.
- `<root>/.gitignore` — Created by init with the standard ignore list; existing files get "effortless.env" appended when missing. Template lines: /**/obj/**/*, /**/bin/**/*, /**/.effortless/**/*, /**/.ssotme/**/*, /**/DSPXml/**/*, /SSoT/__patch.json, /**/.vs/**/*, /**/node_modules/**/*, /**/.vscode/**/*, ssotme.env, effortless.env (verbatim, including the DSPXml line — it is just a template). Step 10: the .effortless line is new; the .ssotme line stays so old clones stay clean.
- `<root>/effortless-rulebook/effortless-rulebook.json` — Seed rulebook {"project":{"name":"<name>"}}.
- `<root>/errors.json` — Per-build failure ledger (schema effortless-build-errors/v1, camelCase). Top-level: schema, generatedAt, startedAt, projectRoot, buildCommand, continueOnError, totalSteps, succeededSteps, failedSteps, skippedSteps, failedStepNames[], steps[] (name, relativePath, status, message, finishedAt), errors[] (StepRecord with exitCode, resolvedVersion, resolvedUrl, transpilerException, cliException chains).
- `<root>/.effortless/<RelativePath>/<toolKey>.zfs` — GZip of the FileSet XML a step last produced (minus self-source entries). toolKey = sanitized POST URL for URL tools, else LowerHyphenName(Name). .effortless is created with the Hidden attribute. Step 10 (D28 option A): a legacy <root>/.ssotme is renamed to .effortless on project load; ledger file names inside are unchanged (step 15 owns them).
- `<root>/.effortless/<RelativePath>/<toolKey>.xml` — Uncompressed copy of the ledger written with -debug. Deleted by clean alongside the .zfs.
- `<root>/.effortless/tempFileSet_<guid>.xml` — Transient copy of the output XML during SaveFileSet. Deleted immediately after extraction.
- `~/.effortless/effortless.key (or effortless.<runAs>.key)` — { EmailAddress, Secret, APIKeys: { account: key } }. EmailAddress/Secret are RabbitMQ-era; kept in the file format for compatibility, no longer used. An empty key is returned when the file is missing. Step 10: migrated from ~/.ssotme/ssotme.key and ssotme.<runAs>.key by UserConfigMigration.
- `~/.effortless/tool_urls.json` — { "<tool>": "<url>" } overrides plus the managed cli-cloud-bridge entry. Indented JSON.
- `~/.effortless/remote_tools/effortless-tools.json` — Cached cli-cloud-bridge list output: transpilerVersions{ "<acct>/<pkg>/<tool>": { "vX": { urls.post, metaData.isHeadVersion, ... } } }, cliUpdateAvailable, latestBridgeVersion, latestCliVersion, fetchedAt, totalCount. fetchedAt is a CLI-stamped UTC instant proving when a valid response last replaced the cache. Both "transpilerVersions" and legacy "transpilers" keys are accepted. fetchedAt advances only after parse and structural validation succeed; refresh failure never makes stale data look current. Step 10: migrated from remote_tools/ssotme-tools.json.
- `~/.effortless/remote_tools/cli_version` — CLI_VERSION at the time of the last refresh. Not by itself a refresh trigger.
- `~/.effortless/remote_tools/effortless.json` — Minimal project so the internal bridge run has a project context. Name "remote_tools". v2 writes only effortless.json here; the legacy remote_tools/ssotme.json is dropped by the step-10 migration (not copied).
- `~/.effortless/bridge_version_index` — Highest latestBridgeVersion.versionIndex applied so far.
- `~/.effortless/update_available.json` — cliUpdateAvailable object from the last refresh (name, version, installLinks{windows,windowsArm,mac,macArm}). Still maintained; the banner that reads it is off by default.
- `~/.effortless/github_version_check.json` — { lastCheck, latestVersion } 24h cache of the GitHub latest release tag. Only reachable through the disabled CheckForUpdateNotice; kept because -upgradeCli shares the parsing.
- `~/.effortless/effortlessapi_token.txt` — Global JWT.
- `~/.effortless/effortlessapi_token_info.json` — { Token, Email, CreatedAt, ExpiresAt(+24h) }.
- `~/.effortless/seed_cache/<seed>/cache/**` — Files copied into a cloned seed. Retained seed cache used by cloneSeed; independent of Airtable metadata guessing.
- `<root>/effortless-seed.json (legacy ssotme-seed.json also accepted; + seed-config-values.json, seed-secrets-values.json)` — Seed replacement tokens ($key$) and interactive answers. Retained explicit $key$ replacement contract. Missing required values fail clearly; Airtable schema guessing is removed.
- `~/.ssotme/` — Legacy user state directory (v1). Step 10: copied (never moved) to ~/.effortless with renames: ssotme.key -> effortless.key, ssotme.<runAs>.key -> effortless.<runAs>.key, remote_tools/ssotme-tools.json -> remote_tools/effortless-tools.json, remote_tools/ssotme.json dropped, everything else same relative path. If both directories exist, ~/.effortless wins and ~/.ssotme is never read again (no merging). Migration failure is fatal.
- `~/.ssotme/MIGRATED-TO-EFFORTLESS` — Records that the legacy directory was copied to ~/.effortless. One line: target path, UTC timestamp, CLI version. The rest of ~/.ssotme is left untouched so a legacy ssotme binary keeps working.
- `<root>/.ssotme/` — Legacy per-project ledger directory. Step 10 (D28 option A): renamed to <root>/.effortless when .effortless is absent. It is gitignored build state so a rename is safe. init writes both ignore lines so old clones stay clean.

## Environment variables and keys

- `EFFORTLESS_CHILD_PROCESS` (process-env) — Set to "1" by the CLI around spawned child "effortless -buildLocal" / "-clean" processes; read by CheckForUpdateNotice to skip update banners in children. Set via Environment.SetEnvironmentVariable on the PARENT process (inherited by the child) and cleared afterwards. Step 10: renamed from SSOTME_CHILD_PROCESS (D31); nothing reads the old name.
- `HOME / USERPROFILE` (process-env) — Resolves ~/.effortless through Environment.GetFolderPath(SpecialFolder.UserProfile). Black-box tests override both to sandbox all user-home state.
- `PATH` (process-env) — Used to locate npm (upgradeCli hint) and by spawned shells to find "effortless" for nested/sub-project builds. New build: child builds spawn the SAME executable by absolute path (Environment.ProcessPath / dotnet + dll) instead of relying on a globally installed "effortless" being on PATH. Console output of the child is unchanged.
- `EFFORTLESS_JWT` (effortless.env) — Project-scoped JWT written by "effortless projectLogin"; takes precedence over the global token and is sent as cliJwt. Read via SsotmeEnvFile.TryLoadFromNearestProject; quotes stripped; comments (#) ignored.
- `{ACCOUNT}_PAT | _API_KEY | _APIKEY | _KEY` (effortless.env) — With -account X, the first matching X_{suffix} value is injected as apiKey=... (case-insensitive key lookup). Precedence: effortless.env beats ~/.effortless/effortless.key APIKeys[X]; both beat a same-named ProjectSetting.
- `{ACCOUNT}_BASEID | _BASE_ID` (effortless.env) — With -account X, injected as baseId=... Same rules as the apiKey mapping.
- `EFFORTLESS_SEED_GITHUB_ACCOUNT` (process-env) — Default GitHub account used by listSeeds and cloneSeed when the invocation does not provide an account. Defaults to ssotme when unset.

## Removed in the rebuild

- `-copilotConnect` — Experimental Baserow/Copilot bridge with a hardcoded tenant id; not built today.
- `-discuss` — Pure RabbitMQ agent loop.
- `-localGuide` — Dead host; superseded by effortless-rulebook-editor.
- `-keyFile` — Zero references.
- `-addTranspiler` — Zero references.
- `-deleteTranspiler` — Zero references.
- `-checkResults` — DSPXml is gone.
- `-createDocs` — DSPXml is gone.
- `-repoUrl` — Zero references.
- `-betaRepo` — Zero references.
- `-skipBuild` — Zero references.
- `-dryRun` — D27: removed. Early build steps write files that later steps read, so a 'run but do not write' build is not a coherent concept - it cannot preview a real multi-step pipeline. Git on a clean tree is the dry run: run the build for real and inspect the diff.
- `-updateUrls` — Dead host; -refreshTools/-upgrade over the remote index supersede it.
- `-legacy` — RabbitMQ only.
- `-latest` — D17: -latest is removed because a version is an attribute of a step, not a command to run. The same outcome is now reached with -pin (to fix a version) or -upgrade/unpin (to clear one); default resolution already tracks HEAD when unpinned. Git history on a clean tree is the audit trail for what changed and when, rather than a runtime command.
