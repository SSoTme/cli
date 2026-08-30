# Step 02 — New solution skeleton + ported core library

**Goal:** `src/Effortless.Cli.Core` exists, targets `net8.0`, contains the *retained* model, codec, project
store and build/clean runners under the `Effortless.Cli` namespace, has **zero** RabbitMQ/SassyMQ/Auth0/
Excel/Html dependencies, and passes the `unit-core` P0 tests. The legacy solution is untouched and the
Step 1 suite still runs against it.

Inputs: `SourceModules` (the move map — `NewPath` + `Notes` per file), `LifecycleStates.NewHome`,
`ProjectFileFields`, `WirePayloadFields`, `ConfigFiles`, `Dependencies`, `ProjectFacts`.

## 1. Solution layout

- `Effortless.Cli.sln` (created in Step 1) gains `src/Effortless.Cli.Core/Effortless.Cli.Core.csproj` and
  `tests/Effortless.Cli.Tests/Effortless.Cli.Tests.csproj`.
- Core csproj: `net8.0`, `<Nullable>disable</Nullable>` (keep the legacy null-handling style for now; enable
  later per file), `<ImplicitUsings>enable</ImplicitUsings>`, package refs **only** `Newtonsoft.Json 13.0.3`
  and `Plossum.CommandLine.Core 0.3.0.14`; `InternalsVisibleTo` both test projects.
- Add `Directory.Build.props` at the root: `TreatWarningsAsErrors=false` for now, `LangVersion latest`,
  common `AssemblyCompany`/`Product`.

## 2. Porting rules (apply mechanically)

1. Copy the legacy method bodies; rename types/namespaces; delete rabbit code. Do **not** "improve" logic.
   Cosmetic modernization (string interpolation, `is null`) is fine; control-flow changes are not.
2. JSON/XML property names are the contract. `ProjectTranspiler`, `ProjectSetting`, the project root
   object, `FileSet`, `FileSetFile`, `TranspilePayload` and its nested `Transpiler`/`TranspileRequest` keep
   their `[JsonProperty]` names and `DefaultValueHandling` exactly as listed in `ProjectFileFields` and
   `WirePayloadFields`. Snapshot tests (`wire-request-snapshot`, `proj-save-format`) enforce it.
3. Every console string comes from `UserMessages` verbatim.
4. Static mutable state in the legacy code (`BuildErrorLog` statics, `CliLog.SuppressFileLog` thread-static,
   `_hasRunRemoteToolsUpdate`, `_cloudBridgeRecoveryAttempted`) may stay static in this step; note each with
   a `// TODO(step-07): instance` comment.

## 3. What to port, file by file

| New file | From | Keep | Drop |
|---|---|---|---|
| `Text/NameHelpers.cs` | `SSOTMEExtensions_core.cs`, `Extensions.cs` | `SafeToString`, `ToCamelString`, `TitleFromCamel`, `ToTitle`, `ToName`, `ToTitleCase`, `SanitizeUrlForFilename`, `StripParamNumber`, `LowerHyphenName` (from `SSoTmeProject`) | everything else in the file |
| `FileSets/GZip.cs` | same | `Zip(string)`, `Zip(byte[])`, `Unzip`, `UnzipToString`, `CopyTo` | — |
| `FileSets/FileSet.cs`, `FileSetFile.cs` | `DataClasses/FileSet*.cs` | all serialized properties (+ `OriginalRelativePath`, `ClearContents`) | `CreatedOn`? keep (serialized) |
| `FileSets/FileSetXml.cs` | same | `ToFileSet(string)`, `ToXml(FileSet)`, `FileSetFilesFromFileSetXml`, `GetFileContents`, `ToSingleTextFileFileSetXml`, `UnwrapCDATA`, `GetFileSetFileContents/BinaryContents` | `ConvertJsonToXml`, `JsonToXml`, `XmlToJson`, `FormatXml` (only DSPXml/BaseHandler used them) |
| `FileSets/FileSetWriter.cs` | same | `SplitFileSetXml` (both overloads), `ProcessFileSetFile`, `SplitFileSetFile` (with the lock-retry loop), `WriteAllText`/`WriteAllBytes` (+ `FileWritten` → `CliLog.Writing`), `FullFromRelative`, `RelativeFromFull`, `FindClosest`, `IsBinaryFile`/`isControlChar` | — |
| `FileSets/FileSetCleaner.cs` | same | `CleanZippedFileSet`, `CleanFileSet`, `CleanFileByRelativeName`, `CleanEmptyFolders`, `GetFullFileName` | — |
| `FileSets/ZfsLedger.cs` | `SSOTMEPayload` | `GetZFSFI`, `SavePreviousFileSet`, `GetCurrentRelativePath`, `RemoveSelfSourceEntries`, `SaveFileSet`, `CleanFileSet` — re-homed as functions taking (project, transpilerKey, cwd) | rabbit base class |
| `FileSets/InputFileSetLoader.cs` | `SSoTmeCLIHandler.LoadInputFiles/ImportFile/SaveOptionalCLIInputs/ValidatePathIsInProjectScope/TryFindNearestProjectRootFrom/WithTrailingSeparator` | all | — |
| `Transpile/TranspilePayload.cs` | `SSOTMEPayload` + `StandardPayload` + `Transpiler`/`TranspileRequest` designers | properties with `Disposition == keep` in `WirePayloadFields` (incl. `PayloadId`, `SenderId`, `SenderName`), nested `Transpiler` (all serialized props) and `TranspileRequest` (all serialized props), `Logs`, `TaskId`, `TaskStatus`, `Exception`, `ErrorMessage`, `GetParameterByName/HasParamNamed/GetParameterByIndex/GetSetting` | rabbit fields; `SSoTmeProject`/`SSoTmeKey` properties become `[JsonIgnore]` runtime-only |
| `Transpile/LogEntry.cs` | `SassySDK/LogEntry.cs` | as-is | — |
| `Transpile/VersionKey.cs` | handler `ParseVersionKey`, `ParseCliVersion`, `ExtractVersionFromUrl` | all | — |
| `Project/ProjectSetting.cs`, `ProjectTranspiler.cs`, `EffortlessProject.cs` | `DataClasses/*` | serialized props + `IsAtPath`, `SyncCommandLineVersion`, `ToString`, `Describe`, `GetProjectRelativePath`; project: `AddSetting`, `RemoveSetting`, `ListSettings`, `Describe`, `GetName`, `Install/Update/UpdateRuntimeOnly/Uninstall/IntegrateTranspiler/FindMatchingTranspilers/ToolNameMatches/GetToolName`, `RemoveUUIds`, `GenerateNewProject`, `GetSSoTmeDI`, `GetZFSDI`, `HiddenPaths/ExpandedPaths/ShowHidden/ShowAllFiles/CurrentPath/SSoTmeProjectFiles` (serialization only) | FileSystemWatcher, `Expand/Collapse/Hide/Show`, `LogMessage` event (keep the Console.WriteLine), `CheckResults`, `CreateDocs`, `LoadInputAndOuputFiles`, Baserow/BOT bridge, `BRIDGE_SERVER_BASE_URL` |
| `Project/ProjectLocator.cs` | `GetProjectFIAt`, `IsValidProjectFile`, `TryToLoad`, `LoadOrFail`, `Load`, `FindNearestProjectRoot` (3 copies exist — one implementation) | all (seed hook behind a `ISeedReplacements` no-op until D1) | — |
| `Project/ProjectFileStore.cs` | `Save`, `Save(DirectoryInfo)`, `GetProjectFileName/FI` | all incl. the JObject merge with `managedKeys` | — |
| `Project/ProjectTranspiler.cs` ctor | `ProjectTranspiler(relativePath, result, pinnedVersion)` | the `Environment.CommandLine` capture + prefix stripping; add `/effortless.cli.dll` to the list; take `(string toolName, string toolDisplayName)` instead of the payload | — |
| `Project/BuildErrorLog.cs` | same | as-is | — |
| `Project/BuildRunner.cs` | `Rebuild/RebuildAll/DoRebuild/CheckIfParentIsRootSeed/FindSSoTmeJsonFiles/BuildSubSSoTmeProjects` + `ProjectTranspiler.Rebuild` | all; the per-step handler creation is injected as `Func<string commandLine, EffortlessProject, bool continueOnError, int>` so Core does not depend on the CLI layer (Step 3 wires it) | `ListenForChangesAndRebuild` (D2) |
| `Project/CleanRunner.cs` | `Clean/CleanAll/RemoveUnusedZFSFiles` + `ProjectTranspiler.Clean` | all | seed reverse replacements (D1) |
| `Project/EmptyFolderPruner.cs` | `RemoveEmptyFolders/RemoveEmptyFoldersInternal/CleanEmptyDirectories` | all | — |
| `Project/ChildProcessRunner.cs` | `DirectoryExtensions.InvokeSSoTmeBuild/InvokeSSoTmeClean/IsIgnored` | all, but spawn `Environment.ProcessPath` (or `dotnet <current dll>`) instead of `effortless` (D6, `keep-modified`); keep the console line `Executing 'effortless -buildLocal' in …` | seed helpers |
| `Config/UserConfigDir.cs`, `KeyFile.cs` | `SSOTMEKey.cs` | `SSoTmeDir` (700), `GetSSoTmeKey(runAs)`, `SetSSoTmeKey` (600), `APIKeys`, `EmailAddress`/`Secret` props (format compat) | `GenerateDefaultKey` constants (return `new` with empty `APIKeys`), `AllKeys` |
| `Config/EnvFile.cs` | `SsotmeEnvFile.cs` + `MyEffortlessAPIService.ReadEnvValue/WriteEnvValue` | all | — |
| `Config/ToolUrls.cs` | handler `TryGetUrlFromFileUrls/SetToolUrl/RemoveToolUrl/GetToolUrlsFilePath` | all | — |
| `Config/JwtStore.cs` | `MyEffortlessAPIService` token methods + `GetEmailFromJwt/IsJwtExpired` | all | quota/register caches |
| `Console/CliLog.cs` | as-is | — | — |
| `Console/ToolLogPrinter.cs` | `DisplayLogEntry` | as-is | — |

Leave `Transpile/TranspileClient.cs`, `ToolResolver.cs`, `RemoteToolsIndex.cs`, `CloudBridgeClient.cs`,
`Auth/`, `Updates/`, `Options/`, `Commands/` for Step 3 (they need the handler state).

## 4. Unit tests to write now (`tests/Effortless.Cli.Tests`)

Implement every `unit-core` P0 row: `unit-sanitize-url`, `unit-lower-hyphen-name`, `unit-version-key-parse`,
`unit-tool-name-matches`, `unit-is-at-path`, `unit-commandline-capture`, `unit-fileset-xml-roundtrip`,
`unit-split-fileset-rules`, `unit-clean-fileset-rules`, `unit-env-file`, `unit-jwt-helpers`,
`unit-project-save-merge`, `unit-project-locator`, `unit-empty-folder-pruner`, `unit-build-error-log`.
Derive expected values by running the **legacy** functions (temporarily reference the legacy Lib project
from a throwaway console app, or read them off the Step 1 goldens) — never from memory.

## 5. Done criteria

- `dotnet build Effortless.Cli.sln` green on macOS/Windows/Linux.
- `dotnet test tests/Effortless.Cli.Tests` green; `TestCases.Status` for the unit rows → `implemented`.
- `grep -r "SassyMQ\|RabbitMQ\|Auth0\|EPPlus\|ExcelDataReader\|HtmlAgilityPack" src/` returns nothing.
- Step 1 suite still green against the legacy dll (nothing under `Windows/` changed).
- Commit `step-02: core library ported (no CLI yet)`.
