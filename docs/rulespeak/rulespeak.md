# 📘 Effortless CLI — RuleSpeak®

_Rulebook for the effortless CLI (a.k.a. ssotme / aicapture / aic): every command-line option, the request-handling state machine, the REST wire contract with transpiler tools, every config file and endpoint, the legacy-to-rebuild move map, the full test plan, and the refactor steps. Authored from the legacy source at commit a8f0f320f4417c58fa931cc4bc8164f79cbbbd97 (tag legacy-final) on 2026-08-30. This file is the single source of truth for the clean REST-only rebuild on branch effortless-cli._

> Declarative business rules rendered from the rulebook. Every statement
> below expresses truth in the business domain — it is neither a procedure
> nor an imperative. The rulebook's formulas are the single source of truth;
> this document is their plain-language reading.

## 1 Business Vocabulary

| Term | Description | Narrative Comment |
|------|-------------|-------------------|
| **Disposition** | The fate of every legacy artifact, option, file, dependency and endpoint in the REST-only rebuild. Every other table with a Disposition column points here. Rows whose NeedsUserConfirmation is true are the open decisions the owner must confirm (or flip) before Step 2 begins; the recommended default is encoded in the row. | — |
| Label | A defined attribute. | _Human-readable label._ |
| Is Retained | True when an empty string. | _True when the artifact carries forward into the new build (possibly modified)._ |
| Needs User Confirmation | True when an empty string. | _True when this is a recommendation, not a settled decision; the owner must confirm before the corresponding refactor step runs._ |
| Description | A defined attribute. | _What this disposition means and how Opus should treat rows that carry it._ |
| Name | The same as its disposition ID. | _Display alias for the row; mirrors DispositionId._ |
| Count of Cli Options | The number of cli options related to the disposition. | _How many CLI options carry this disposition._ |
| Count of Source Modules | The number of source modules related to the disposition. | _How many legacy source modules carry this disposition._ |
| Count of Http Endpoints | The number of http endpoints related to the disposition. | _How many remote endpoints carry this disposition._ |
| **Option Category** | Functional grouping of CLI options, used to organize the generated help/reference docs and the test suites. | — |
| Label | A defined attribute. | _Heading used in generated documentation._ |
| Sort Order | A defined attribute. | _Display order in generated docs._ |
| Description | A defined attribute. | _What kind of options live here._ |
| Name | The same as its option category ID. | _Display alias for the row; mirrors OptionCategoryId._ |
| Count of Cli Options | The number of cli options related to the option category. | _Number of options in this category._ |
| **Cli Option** | Every command-line option the legacy CLI declares (Plossum CommandLineOption attributes), with its verbatim help text, bareword verb forms (FixParameters), how it is handled, and its fate. This table is the source of truth for the generated options class, the generated CLI reference, the -help output, and test coverage (CountOfTestCases). | — |
| Flag | A defined attribute. | _Primary flag as typed by the user._ |
| Aliases | A defined attribute. | _Comma-separated Plossum aliases (verbatim from the designer file; empty when none)._ |
| Bareword Forms | A defined attribute. | _Comma-separated bare verbs accepted as the FIRST positional argument (from FixParameters; matched case-insensitively after ToLower). Empty when none._ |
| Value Type | A defined attribute. | _bool, string, int or list (List<string>)._ |
| Help Text | A defined attribute. | _Verbatim Description shown by -help. MUST be preserved character-for-character for retained options._ |
| Category | A defined attribute. | _Functional grouping._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Is Command | True when an empty string. | _True when the option selects an action (a verb); false when it modifies another action._ |
| Consumes Next Arg As Tool | True when an empty string. | _True when the bareword form takes the next positional argument as the tool/value (FixParameters sets transpiler = args[1] for every matched verb; for setUrl/viewUrl/removeUrl the value is also copied into the option)._ |
| Legacy Ref Count | A defined attribute. | _Number of "this.<option>" references in the legacy handler/project/payload code (0 means declared but never read)._ |
| Handler Notes | A defined attribute. | _Where and how the option is consumed in the legacy code._ |
| Disposition Reason | A defined attribute. | _Why it gets this disposition; for keep-modified, the exact change._ |
| Description | A defined attribute. | _Plain-English meaning for docs._ |
| Name | The same as its flag. | _Display alias for the row; mirrors Flag._ |
| Is Retained | True when the linked disposition is retained. | _Whether the option survives into the new build._ |
| Needs User Confirmation | True when the linked disposition is needs user confirmation. | _Whether the owner must confirm this disposition._ |
| Category Label | Taken from the linked category. | _Heading for docs._ |
| Count of Test Cases | The number of test cases related to the cli option. | _Number of planned test cases whose primary subject is this option._ |
| Is Tested | True when the count of test cases is greater than 0. | _True when at least one test case targets this option._ |
| Is Retained But Untested | True when all of the following hold: the retained flag is set and the tested flag is not set. | _Coverage gap flag: retained options with no test are not allowed to reach Step 2._ |
| **Lifecycle Phas** | Coarse phases of one CLI process, in execution order. LifecycleStates and DispatchRules hang off these. | — |
| Sort Order | A defined attribute. | _Execution order within a process._ |
| Description | A defined attribute. | _What happens in this phase._ |
| Name | The same as its lifecycle phase ID. | _Display alias for the row; mirrors LifecyclePhaseId._ |
| Count of States | The number of lifecycle states related to the lifecycle phas. | _Number of fine-grained states in this phase._ |
| Count of Dispatch Rules | The number of dispatch rules related to the lifecycle phas. | _Number of dispatch rules evaluated in this phase._ |
| **Lifecycle State** | Fine-grained states of one CLI process (and of the build/clean loops it may enter). Together with StateTransitions this is the executable-level state machine the rebuild must reproduce. NewHome names the class/file in the rebuilt solution that owns the state. | — |
| Phase | A defined attribute. | _Coarse phase._ |
| Sort Order | A defined attribute. | _Nominal order within the phase._ |
| Entry | A defined attribute. | _What the state does on entry._ |
| Implemented in | A defined attribute. | _Legacy method(s)._ |
| New Home | A defined attribute. | _Owning type in src/Effortless.Cli.Core (proposed)._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Invariants and quirks that tests pin down._ |
| Name | The same as its lifecycle state ID. | _Display alias for the row; mirrors LifecycleStateId._ |
| Phase Sort Order | Taken from the linked phase. | _Order of the parent phase (for sorting the whole machine)._ |
| Count of Outgoing Transitions | The number of state transitions related to the lifecycle state. | _Number of transitions leaving this state._ |
| Count of Incoming Transitions | The number of state transitions related to the lifecycle state. | _Number of transitions entering this state._ |
| **State Transition** | Edges of the lifecycle state machine. Condition is the guard; an empty guard means unconditional. | — |
| From State | A defined attribute. | _Source state._ |
| To State | A defined attribute. | _Target state._ |
| Condition | A defined attribute. | _Guard._ |
| Description | A defined attribute. | _Notes._ |
| Name | Computed as the from state, followed by “ -> ”, followed by the to state. | _Edge label._ |
| **Dispatch Rule** | The ordered decision chain that selects exactly one action per invocation. Rows are evaluated in SortOrder within a Phase; the first whose Condition holds wins (legacy if/else-if chains in ParseCommand and TranspileProject). This IS the state machine of request handling: the new dispatcher must reproduce this order exactly, including the surprising ones (init implies build; describe beats build; clean has three branches). | — |
| Phase | A defined attribute. | _Which chain this rule belongs to._ |
| Sort Order | A defined attribute. | _Evaluation order within the phase._ |
| Condition | A defined attribute. | _Condition (legacy C# expression, lightly paraphrased)._ |
| Action | A defined attribute. | _What happens when it matches._ |
| Project Required | True when an empty string. | _True when the action calls GetProjectOrThrow (ProjectNotConfiguredException => silent exit -1 when no project)._ |
| Suppresses Transpile | True when an empty string. | _True when the rule ends the invocation without a transpile (SuppressTranspile / continueToLoad=false)._ |
| Exit Code | A defined attribute. | _Exit code produced (0, -1, or "flow" when execution continues)._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Notes for the implementer._ |
| Name | The same as its dispatch rule ID. | _Display alias for the row; mirrors DispatchRuleId._ |
| **Tool Resolution Rule** | How a tool argument becomes a POST URL, a resolved version key, and the "cli:>" label. Applied in Precedence order; the first source that yields a URL wins, except that a tool_urls.json entry for the raw name ALWAYS overrides the URL afterwards (rule 5). | — |
| Precedence | A defined attribute. | _Evaluation order._ |
| Source | A defined attribute. | _Where the URL comes from._ |
| Condition | A defined attribute. | _When the rule applies._ |
| Result | A defined attribute. | _What is set (targetUrl, transpiler, ResolvedVersion*, account)._ |
| Version Label | A defined attribute. | _The suffix printed after "cli:> <tool> <version>"._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Details._ |
| Name | The same as its tool resolution rule ID. | _Display alias for the row; mirrors ToolResolutionRuleId._ |
| **Retry Rule** | The transient-failure handling matrix of the HTTP transpile request (ProxyRequest). The loop runs while elapsed < waitTimeout and retryCount < 10. | — |
| Trigger | A defined attribute. | _Exception or HTTP status that triggers the rule._ |
| Action | A defined attribute. | _What the client does._ |
| Delay Ms | A defined attribute. | _Delay before retrying (0 when no retry)._ |
| Max Attempts | A defined attribute. | _Attempt budget specific to this trigger (10 = shared retry budget)._ |
| Message | A defined attribute. | _Console line printed (with [tool] prefix where shown)._ |
| Description | A defined attribute. | _Notes._ |
| Name | The same as its retry rule ID. | _Display alias for the row; mirrors RetryRuleId._ |
| **Wire Payload Field** | The REST wire contract between the CLI and a transpiler tool: a single JSON POST (request serialized by System.Net.Http.Json with JsonSerializerDefaults.Web => camelCase names, byte[] as base64) and a JSON response parsed case-insensitively by Newtonsoft. ToolFileCount is how many files in the Versioned-Stable-SSoTme-Tools repo mention the field (evidence of consumption, measured 2026-08-30). | — |
| Direction | A defined attribute. | _request, response, or both._ |
| JSON Name | A defined attribute. | _JSON property name (request: camelCase as emitted; response: as the tool emits it, matched case-insensitively)._ |
| Clr Type | A defined attribute. | _CLR type in the DTO._ |
| Source or Sink | A defined attribute. | _Where the value comes from (request) or goes (response)._ |
| Tool File Count | A defined attribute. | _Number of tool source files referencing the name (any casing)._ |
| Required | True when an empty string. | _True when at least one live tool depends on it._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Notes._ |
| Name | The same as its JSON name. | _Display alias for the row; mirrors JsonName._ |
| **Http Endpoint** | Every remote endpoint the legacy CLI can call, with liveness checked on 2026-08-30 (HTTP status or DNS failure). | — |
| Method | A defined attribute. | _HTTP method._ |
| URL Pattern | A defined attribute. | _URL or pattern._ |
| Purpose | A defined attribute. | _What it is for._ |
| Used by Option | A defined attribute. | _Option(s)/states that call it._ |
| Liveness On20260830 | A defined attribute. | _alive (HTTP 200), dead (DNS/connection failure), or n/a._ |
| Timeout Ms | A defined attribute. | _Client timeout used._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Notes._ |
| Name | The same as its http endpoint ID. | _Display alias for the row; mirrors HttpEndpointId._ |
| **File Scope** | Where a config/state file lives relative to the user and the project. | — |
| Description | A defined attribute. | _Meaning of the scope._ |
| Name | The same as its file scope ID. | _Display alias for the row; mirrors FileScopeId._ |
| Count of Config Files | The number of config files related to the file scope. | _Number of files in this scope._ |
| **Config File** | Every file the CLI reads or writes outside of transpiler output. Paths are literal; <root> is the project root, ~ is the user profile dir. | — |
| Path | A defined attribute. | _Literal path pattern._ |
| Scope | A defined attribute. | _Where it lives._ |
| Format | A defined attribute. | _json, text, xml, gzip-xml, dotenv, gitignore._ |
| Purpose | A defined attribute. | _What it holds._ |
| Read by | A defined attribute. | _Commands/states that read it._ |
| Written by | A defined attribute. | _Commands/states that write it._ |
| Is Secret | True when an empty string. | _Contains credentials._ |
| Unix Mode | A defined attribute. | _chmod applied on macOS/Linux when written ("" when none)._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Schema notes and quirks._ |
| Name | The same as its config file ID. | _Display alias for the row; mirrors ConfigFileId._ |
| **Project File Field** | The effortless.json schema as actually serialized by the legacy CLI (Newtonsoft with DefaultValueHandling). Byte-level compatibility of this file is a hard requirement: existing projects must load and re-save identically. | — |
| Owner | A defined attribute. | _Project, ProjectTranspiler, or ProjectSetting._ |
| JSON Name | A defined attribute. | _Property name in the file._ |
| Clr Type | A defined attribute. | _CLR type._ |
| Serialization Rule | A defined attribute. | _IgnoreAndPopulate (omitted when default), Include (always), Ignore (never), or removed-on-save._ |
| Is Model Owned | True when an empty string. | _True for ProjectTranspiler properties the model owns: Save() must NOT resurrect them from the on-disk copy when the fresh serialization omitted them (the -upgrade/PinnedVersion gotcha)._ |
| Written When | A defined attribute. | _Which operations set it._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Meaning and quirks._ |
| Name | The same as its project file field ID. | _Display alias for the row; mirrors ProjectFileFieldId._ |
| **User Message** | Console strings that define the observable behavior. Templates use {n} placeholders as in the code; "[cli] " is the CliLog prefix (blue). Golden messages are asserted verbatim by the black-box tests. | — |
| Template | A defined attribute. | _The text (verbatim where possible)._ |
| Color | A defined attribute. | _Console color or "default"._ |
| Trigger | A defined attribute. | _When it is printed._ |
| Stream | A defined attribute. | _stdout (everything is stdout in the legacy CLI)._ |
| Is Golden for Tests | True when an empty string. | _Asserted verbatim by at least one test._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Notes._ |
| Name | The same as its user message ID. | _Display alias for the row; mirrors UserMessageId._ |
| **Exit Code** | Process exit codes the CLI can produce and what they mean. Note that -1 becomes 255 on POSIX shells. | — |
| Process Exit Code | A defined attribute. | _The integer returned from Main (or passed to Environment.Exit)._ |
| Meaning | A defined attribute. | _What the code signals._ |
| Produced by | A defined attribute. | _Where in the legacy code the value originates._ |
| Description | A defined attribute. | _Extra detail for tests._ |
| Name | The same as its exit code ID. | _Display alias for the row; mirrors ExitCodeId._ |
| **Env Variable** | Environment variables and env-file keys the CLI reads or sets. | — |
| Source | A defined attribute. | _process-env, effortless.env, or build-define._ |
| Purpose | A defined attribute. | _How it is used._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Details and quirks._ |
| Name | The same as its env variable ID. | _Display alias for the row; mirrors EnvVariableId._ |
| **Entry Point** | Every way the CLI binary gets invoked, and how argv reaches the handler in each. | — |
| Kind | A defined attribute. | _npm-shim, msi, pkg, pip, or dotnet-dll._ |
| Invocation | A defined attribute. | _How the user runs it._ |
| Arg Passing | A defined attribute. | _How arguments reach Program.Main._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Description | A defined attribute. | _Details and quirks._ |
| Name | The same as its entry point ID. | _Display alias for the row; mirrors EntryPointId._ |
| **Dependency** | Package dependencies of the legacy build and their fate. | — |
| Ecosystem | A defined attribute. | _nuget or npm._ |
| Legacy Version | A defined attribute. | _Version in the legacy csproj/package.json._ |
| New Version | A defined attribute. | _Version in the rebuild (empty when dropped)._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| Reason | A defined attribute. | _Why it is kept or dropped._ |
| Description | A defined attribute. | _What it was used for._ |
| Name | The same as its dependency ID. | _Display alias for the row; mirrors DependencyId._ |
| **Source Module** | The move map: every legacy file or directory, its fate, and where its retained logic lands in the rebuilt solution. This is the checklist for Steps 2-4; nothing may be left behind un-dispositioned. | — |
| Legacy Path | A defined attribute. | _Path in the legacy repo (a8f0f32)._ |
| Kind | A defined attribute. | _dir or file._ |
| Approx Lines | A defined attribute. | _Approximate line count (0 for dirs/binary)._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| New Path | A defined attribute. | _Destination in the rebuilt repo ("" when dropped)._ |
| Notes | A defined attribute. | _What to keep/strip when porting._ |
| Description | A defined attribute. | _What it is._ |
| Name | The same as its legacy path. | _Display alias for the row; mirrors LegacyPath._ |
| Is Retained | True when the linked disposition is retained. | _Whether any of it survives._ |
| **Devops Pipeline** | CI/CD and release plumbing that exists today and where it goes. | — |
| Legacy Path | A defined attribute. | _File in the legacy repo._ |
| Trigger | A defined attribute. | _What starts it._ |
| Produces | A defined attribute. | _Artifacts / side effects._ |
| Disposition | A defined attribute. | _Fate in the rebuild._ |
| New Path | A defined attribute. | _Location in the rebuilt repo (empty when dropped)._ |
| Description | A defined attribute. | _Notes._ |
| Name | The same as its devops pipeline ID. | _Display alias for the row; mirrors DevopsPipelineId._ |
| **Project Fact** | Single-valued facts about the CLI that the code, docs and tests must agree on. These are the constants of the system. | — |
| Value | A defined attribute. | _The value (always a string; parse as needed)._ |
| Category | A defined attribute. | _Grouping: identity, versioning, timing, network, layout, naming._ |
| Description | A defined attribute. | _Why it matters / where it is used._ |
| Name | The same as its project fact ID. | _Display alias for the row; mirrors ProjectFactId._ |
| **Test Suite** | Test suites. The black-box (e2e-*) suites are the characterization suite: written in Step 1 against the LEGACY binary and required to stay green against the rebuilt binary; the binary under test is selected with the EFFORTLESS_CLI_UNDER_TEST environment variable. unit-* and contract-* suites target the new code from Step 2 on. | — |
| Harness | A defined attribute. | _xunit project that hosts it._ |
| Kind | A defined attribute. | _black-box, unit, contract, or devops._ |
| Runs in Ci | True when an empty string. | _Part of the default CI matrix._ |
| Needs Network | True when an empty string. | _Needs internet (excluded from CI by default; run manually)._ |
| Description | A defined attribute. | _Scope and fixtures._ |
| Name | The same as its test suite ID. | _Display alias for the row; mirrors TestSuiteId._ |
| Count of Test Cases | The number of test cases related to the test suite. | _Number of planned cases._ |
| **Test Cas** | The comprehensive test list. Each case is black-box unless its suite says otherwise. Given/When/Then are the contract; Step 1 implements every P0/P1 e2e case against the legacy binary and records golden output where IsGoldenForTests messages apply. Status moves planned -> implemented -> legacy-green -> rebuild-green. | — |
| Suite | A defined attribute. | _Owning suite._ |
| Primary Option | A defined attribute. | _The option this case primarily exercises (drives coverage)._ |
| Title | A defined attribute. | _Short title._ |
| Given | A defined attribute. | _Preconditions / fixture._ |
| When | A defined attribute. | _Command(s) run._ |
| Then | A defined attribute. | _Assertions._ |
| Priority | A defined attribute. | _P0 must pass before Step 2; P1 before Step 4; P2 before cutover._ |
| Interactive | True when an empty string. | _Feeds stdin._ |
| Slow | True when an empty string. | _Takes > 10 s (timeouts/retries); tagged so CI can run them in a separate job._ |
| Status | A defined attribute. | _planned, planned-step-03a (new behavior intentionally deferred until that step), implemented, legacy-green, rebuild-green, or blocked-by-decision._ |
| Description | A defined attribute. | _Notes / fixture names._ |
| Name | The same as its test case ID. | _Display alias for the row; mirrors TestCaseId._ |
| Suite Kind | Taken from the linked suite. | _black-box/unit/contract/devops._ |
| Option Disposition | Taken from the linked primary option. | _Disposition of the primary option (tests for dropped options are deleted in Step 4)._ |
| **Refactor Step** | The pieces of the puzzle, in order. Each step has a full instruction document under docs/refactor-plan/. A step may not start until DependsOn is done and its DoneCriteria are objectively verifiable (tests, diffs, CI). | — |
| Sort Order | A defined attribute. | _Execution order._ |
| Title | A defined attribute. | _Short title._ |
| Goal | A defined attribute. | _One-sentence outcome._ |
| Depends on | A defined attribute. | _Prerequisite step (null for the first)._ |
| Instructions Path | A defined attribute. | _Markdown file with the full instructions._ |
| Done Criteria | A defined attribute. | _Objective completion checks._ |
| Owner | A defined attribute. | _fable (done in the authoring session) or opus (subsequent sessions)._ |
| Status | A defined attribute. | _done, ready, or blocked._ |
| Description | A defined attribute. | _Scope summary._ |
| Name | The same as its refactor step ID. | _Display alias for the row; mirrors RefactorStepId._ |

## 2 Fact Types

- a **cli option** references exactly one **option category**
- a **cli option** references exactly one **disposition**
- a **lifecycle state** references exactly one **lifecycle phas**
- a **lifecycle state** references exactly one **disposition**
- a **state transition** references exactly one **lifecycle state**
- a **dispatch rule** references exactly one **lifecycle phas**
- a **dispatch rule** references exactly one **disposition**
- a **tool resolution rule** references exactly one **disposition**
- a **wire payload field** references exactly one **disposition**
- a **http endpoint** references exactly one **disposition**
- a **config file** references exactly one **file scope**
- a **config file** references exactly one **disposition**
- a **project file field** references exactly one **disposition**
- a **user message** references exactly one **disposition**
- an **env variable** references exactly one **disposition**
- an **entry point** references exactly one **disposition**
- a **dependency** references exactly one **disposition**
- a **source module** references exactly one **disposition**
- a **devops pipeline** references exactly one **disposition**
- a **test cas** references exactly one **test suite**
- a **test cas** references exactly one **cli option**
- a **refactor step** may reference one **refactor step**

## 3 Operative Rules

_Operative rules state what the business **obliges**, **prohibits**, or
advises (**should**). Structural rules come from required fields and foreign keys;
semantic rules come from the Constraints table, each keyed on a boolean the rulebook
already computes (cross-referenced as DR-N in the Definitional Rules below)._

### Structural Constraints (from the schema)

- A cli option **must** reference exactly one option category as its category.
- A cli option **must** reference exactly one disposition.
- A lifecycle state **must** reference exactly one lifecycle phas as its phase.
- A lifecycle state **must** reference exactly one disposition.
- A state transition **must** reference exactly one lifecycle state as its from state.
- A state transition **must** reference exactly one lifecycle state as its to state.
- A dispatch rule **must** reference exactly one lifecycle phas as its phase.
- A dispatch rule **must** reference exactly one disposition.
- A tool resolution rule **must** reference exactly one disposition.
- A wire payload field **must** reference exactly one disposition.
- A http endpoint **must** reference exactly one disposition.
- A config file **must** reference exactly one file scope as its scope.
- A config file **must** reference exactly one disposition.
- A project file field **must** reference exactly one disposition.
- A user message **must** reference exactly one disposition.
- An env variable **must** reference exactly one disposition.
- An entry point **must** reference exactly one disposition.
- A dependency **must** reference exactly one disposition.
- A source module **must** reference exactly one disposition.
- A devops pipeline **must** reference exactly one disposition.
- A test cas **must** reference exactly one test suite as its suite.
- A test cas **must** reference exactly one cli option as its primary option.

## 4 Definitional Rules

_All statements express truth in the business domain; they are neither
procedures nor imperatives. "iff" is avoided in favor of "only if" so a
one-directional necessity is not mistaken for an equivalence. A
**⚠︎ mechanical** chip marks a rule whose deterministic wording is faithful
but clunky — a flag for an optional downstream reword pass, not a defect._

| ID | Declarative rule |
|----|------------------|
| **DR-1 Name** | A disposition's name is the same as its disposition ID. |
| **DR-2 Count of Cli Options** | A disposition's count of cli options is the number of cli options related to the disposition. |
| **DR-3 Count of Source Modules** | A disposition's count of source modules is the number of source modules related to the disposition. |
| **DR-4 Count of Http Endpoints** | A disposition's count of http endpoints is the number of http endpoints related to the disposition. |
| **DR-5 Name** | An option category's name is the same as its option category ID. |
| **DR-6 Count of Cli Options** | An option category's count of cli options is the number of cli options related to the option category. |
| **DR-7 Name** | A cli option's name is the same as its flag. |
| **DR-8 Is Retained** | A cli option's is retained when the linked disposition is retained. |
| **DR-9 Needs User Confirmation** | A cli option's needs user confirmation when the linked disposition is needs user confirmation. |
| **DR-10 Category Label** | A cli option's category label — taken from the linked category. |
| **DR-11 Count of Test Cases** | A cli option's count of test cases is the number of test cases related to the cli option. |
| **DR-12 Is Tested** | A cli option is considered tested if the count of test cases is greater than 0. |
| **DR-13 Is Retained But Untested** | A cli option is considered retained-but-untested if all of the following hold: the retained flag is set and the tested flag is not set. |
| **DR-14 Name** | A lifecycle phas's name is the same as its lifecycle phase ID. |
| **DR-15 Count of States** | A lifecycle phas's count of states is the number of lifecycle states related to the lifecycle phas. |
| **DR-16 Count of Dispatch Rules** | A lifecycle phas's count of dispatch rules is the number of dispatch rules related to the lifecycle phas. |
| **DR-17 Name** | A lifecycle state's name is the same as its lifecycle state ID. |
| **DR-18 Phase Sort Order** | A lifecycle state's phase sort order — taken from the linked phase. |
| **DR-19 Count of Outgoing Transitions** | A lifecycle state's count of outgoing transitions is the number of state transitions related to the lifecycle state. |
| **DR-20 Count of Incoming Transitions** | A lifecycle state's count of incoming transitions is the number of state transitions related to the lifecycle state. |
| **DR-21 Name** | A state transition's name is computed as the from state, followed by “ -> ”, followed by the to state. |
| **DR-22 Name** | A dispatch rule's name is the same as its dispatch rule ID. |
| **DR-23 Name** | A tool resolution rule's name is the same as its tool resolution rule ID. |
| **DR-24 Name** | A retry rule's name is the same as its retry rule ID. |
| **DR-25 Name** | A wire payload field's name is the same as its JSON name. |
| **DR-26 Name** | A http endpoint's name is the same as its http endpoint ID. |
| **DR-27 Name** | A file scope's name is the same as its file scope ID. |
| **DR-28 Count of Config Files** | A file scope's count of config files is the number of config files related to the file scope. |
| **DR-29 Name** | A config file's name is the same as its config file ID. |
| **DR-30 Name** | A project file field's name is the same as its project file field ID. |
| **DR-31 Name** | A user message's name is the same as its user message ID. |
| **DR-32 Name** | An exit code's name is the same as its exit code ID. |
| **DR-33 Name** | An env variable's name is the same as its env variable ID. |
| **DR-34 Name** | An entry point's name is the same as its entry point ID. |
| **DR-35 Name** | A dependency's name is the same as its dependency ID. |
| **DR-36 Name** | A source module's name is the same as its legacy path. |
| **DR-37 Is Retained** | A source module's is retained when the linked disposition is retained. |
| **DR-38 Name** | A devops pipeline's name is the same as its devops pipeline ID. |
| **DR-39 Name** | A project fact's name is the same as its project fact ID. |
| **DR-40 Name** | A test suite's name is the same as its test suite ID. |
| **DR-41 Count of Test Cases** | A test suite's count of test cases is the number of test cases related to the test suite. |
| **DR-42 Name** | A test cas's name is the same as its test case ID. |
| **DR-43 Suite Kind** | A test cas's suite kind — taken from the linked suite. |
| **DR-44 Option Disposition** | A test cas's option disposition — taken from the linked primary option. |
| **DR-45 Name** | A refactor step's name is the same as its refactor step ID. |

## 5 Traceability to Schema

_The expression column is the rule's definition in RuleSpeak® notation —
the same logic the rulebook stores, written for a business reader._

| Schema element | Kind | Expression |
|----------------|------|------------|
| **Dispositions.Name** | formula | `DispositionId` |
| **Dispositions.CountOfCliOptions** | rollup | `Count(CliOptions via Disposition)` |
| **Dispositions.CountOfSourceModules** | rollup | `Count(SourceModules via Disposition)` |
| **Dispositions.CountOfHttpEndpoints** | rollup | `Count(HttpEndpoints via Disposition)` |
| **OptionCategories.Name** | formula | `OptionCategoryId` |
| **OptionCategories.CountOfCliOptions** | rollup | `Count(CliOptions via Category)` |
| **CliOptions.Name** | formula | `Flag` |
| **CliOptions.IsRetained** | lookup | `Lookup(Dispositions.IsRetained via Disposition)` |
| **CliOptions.NeedsUserConfirmation** | lookup | `Lookup(Dispositions.NeedsUserConfirmation via Disposition)` |
| **CliOptions.CategoryLabel** | lookup | `Lookup(OptionCategories.Label via Category)` |
| **CliOptions.CountOfTestCases** | rollup | `Count(TestCases via PrimaryOption)` |
| **CliOptions.IsTested** | formula | `CountOfTestCases > 0` |
| **CliOptions.IsRetainedButUntested** | formula | `And(IsRetained, Not(IsTested))` |
| **LifecyclePhases.Name** | formula | `LifecyclePhaseId` |
| **LifecyclePhases.CountOfStates** | rollup | `Count(LifecycleStates via Phase)` |
| **LifecyclePhases.CountOfDispatchRules** | rollup | `Count(DispatchRules via Phase)` |
| **LifecycleStates.Name** | formula | `LifecycleStateId` |
| **LifecycleStates.PhaseSortOrder** | lookup | `Lookup(LifecyclePhases.SortOrder via Phase)` |
| **LifecycleStates.CountOfOutgoingTransitions** | rollup | `Count(StateTransitions via FromState)` |
| **LifecycleStates.CountOfIncomingTransitions** | rollup | `Count(StateTransitions via ToState)` |
| **StateTransitions.Name** | formula | `FromState & " -> " & ToState` |
| **DispatchRules.Name** | formula | `DispatchRuleId` |
| **ToolResolutionRules.Name** | formula | `ToolResolutionRuleId` |
| **RetryRules.Name** | formula | `RetryRuleId` |
| **WirePayloadFields.Name** | formula | `JsonName` |
| **HttpEndpoints.Name** | formula | `HttpEndpointId` |
| **FileScopes.Name** | formula | `FileScopeId` |
| **FileScopes.CountOfConfigFiles** | rollup | `Count(ConfigFiles via Scope)` |
| **ConfigFiles.Name** | formula | `ConfigFileId` |
| **ProjectFileFields.Name** | formula | `ProjectFileFieldId` |
| **UserMessages.Name** | formula | `UserMessageId` |
| **ExitCodes.Name** | formula | `ExitCodeId` |
| **EnvVariables.Name** | formula | `EnvVariableId` |
| **EntryPoints.Name** | formula | `EntryPointId` |
| **Dependencies.Name** | formula | `DependencyId` |
| **SourceModules.Name** | formula | `LegacyPath` |
| **SourceModules.IsRetained** | lookup | `Lookup(Dispositions.IsRetained via Disposition)` |
| **DevopsPipelines.Name** | formula | `DevopsPipelineId` |
| **ProjectFacts.Name** | formula | `ProjectFactId` |
| **TestSuites.Name** | formula | `TestSuiteId` |
| **TestSuites.CountOfTestCases** | rollup | `Count(TestCases via Suite)` |
| **TestCases.Name** | formula | `TestCaseId` |
| **TestCases.SuiteKind** | lookup | `Lookup(TestSuites.Kind via Suite)` |
| **TestCases.OptionDisposition** | lookup | `Lookup(CliOptions.Disposition via PrimaryOption)` |
| **RefactorSteps.Name** | formula | `RefactorStepId` |

---

_This document is rendered in **RuleSpeak®**, the declarative business-rule
notation created by **Ronald G. Ross**, and follows the conventions of
**SBVR** (Semantics of Business Vocabulary and Business Rules). With thanks to
Ronald G. Ross for RuleSpeak® and his foundational work on business rules —
[www.RonRoss.info](https://www.RonRoss.info)._
