/*******************************************
 License:    Mozilla Public License 2.0
 *******************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SSoTme.OST.Lib.Extensions;

namespace SSoTme.OST.Lib.DataClasses
{
    /// <summary>
    /// Marker exception meaning: "a build step failed, and BuildErrorLog has ALREADY
    /// recorded the full detail for it." Thrown by ProjectTranspiler.Rebuild so that
    /// SSoTmeProject.DoRebuild — the one place that knows whether -continueOnError is
    /// in effect — can decide to keep going or bail, without either (a) duplicating the
    /// record, or (b) having to reconstruct which step blew up from the exception text.
    /// </summary>
    public class TranspilerStepFailedException : Exception
    {
        public TranspilerStepFailedException(string message) : base(message) { }
        public TranspilerStepFailedException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Collects per-step failures across a single `effortless build` run and, at the end
    /// of that run, writes them to &lt;projectRoot&gt;/errors.json.
    ///
    /// Why this exists: with -continueOnError, a failing step no longer aborts the build,
    /// so its exception details would otherwise be *gone* — buried mid-log at best, and
    /// (for tools that report a blank message) invisible at worst. The console output a
    /// transpiler produces is deliberately sanitized/short; this file is the unsanitized
    /// record: exit code, full exception chain, stack traces, the command line that ran,
    /// and the resolved tool version/URL that produced it.
    ///
    /// Contract for consumers (e.g. effortless-rulebook-editor's boot server):
    ///   - errors.json EXISTS  => the last build in this project had at least one failure.
    ///   - errors.json ABSENT  => the last build was clean. A clean build DELETES a stale
    ///                            file rather than leaving yesterday's failures lying around
    ///                            to be re-reported forever.
    /// </summary>
    public static class BuildErrorLog
    {
        public const string ErrorsFileName = "errors.json";
        public const string SchemaId = "effortless-build-errors/v1";

        private static bool _active;
        private static string _projectRoot;
        private static bool _continueOnError;
        private static string _buildCommand;
        private static DateTime _startedAtUtc;
        private static readonly List<StepRecord> _steps = new List<StepRecord>();

        // errors.json is read by non-.NET consumers (the rulebook-editor boot server is
        // plain Node, and this is the contract any future dashboard reads), so the whole
        // document is camelCase — including the StepRecord/ExceptionRecord bodies, which
        // would otherwise serialize PascalCase and make the file half one convention.
        private static readonly JsonSerializerSettings CamelCase = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static bool IsActive { get { return _active; } }
        public static bool HasFailures { get { return _steps.Any(s => s.Status == "failed"); } }
        public static int FailureCount { get { return _steps.Count(s => s.Status == "failed"); } }

        /// <summary>
        /// Open a ledger for one build run. Safe to call when projectRoot is unknown
        /// (null/empty) — the ledger still collects and prints, it just has nowhere to
        /// write the file, and says so instead of throwing.
        /// </summary>
        public static void Begin(string projectRoot, bool continueOnError, string buildCommand)
        {
            _active = true;
            _projectRoot = projectRoot;
            _continueOnError = continueOnError;
            _buildCommand = buildCommand;
            _startedAtUtc = DateTime.UtcNow;
            _steps.Clear();
        }

        public static void RecordSuccess(ProjectTranspiler pt)
        {
            if (!_active || ReferenceEquals(pt, null)) return;
            _steps.Add(new StepRecord
            {
                Name = pt.Name,
                RelativePath = pt.RelativePath,
                CommandLine = pt.CommandLine,
                Status = "succeeded",
                FinishedAt = DateTime.UtcNow.ToString("o"),
            });
        }

        public static void RecordSkipped(ProjectTranspiler pt, string reason)
        {
            if (!_active || ReferenceEquals(pt, null)) return;
            _steps.Add(new StepRecord
            {
                Name = pt.Name,
                RelativePath = pt.RelativePath,
                CommandLine = pt.CommandLine,
                Status = "skipped",
                Message = reason,
                FinishedAt = DateTime.UtcNow.ToString("o"),
            });
        }

        /// <summary>
        /// Record a failed step with everything we know about it. `transpilerException`
        /// is the exception the TOOL reported back over the wire (payload.Exception, which
        /// carries the tool's own stack); `thrownException` is anything the CLI itself
        /// threw while running the step. Either may be null; both are captured when present.
        /// </summary>
        public static void RecordFailure(
            ProjectTranspiler pt,
            int exitCode,
            Exception transpilerException,
            Exception thrownException,
            string resolvedVersion,
            string resolvedUrl)
        {
            if (!_active) return;

            var message = FirstNonBlank(
                transpilerException?.Message,
                thrownException?.Message,
                exitCode != 0 ? $"the tool exited with code {exitCode} but reported no error message" : null,
                "the tool failed but reported no error message");

            _steps.Add(new StepRecord
            {
                Name = pt?.Name,
                RelativePath = pt?.RelativePath,
                CommandLine = pt?.CommandLine,
                Status = "failed",
                ExitCode = exitCode,
                Message = message,
                ResolvedVersion = resolvedVersion,
                ResolvedUrl = resolvedUrl,
                FinishedAt = DateTime.UtcNow.ToString("o"),
                TranspilerException = Describe(transpilerException),
                CliException = Describe(thrownException),
            });
        }

        /// <summary>
        /// Close the ledger: print a human summary, then write (or delete) errors.json.
        /// Returns the path written, or null if nothing was written.
        /// </summary>
        public static string Finish()
        {
            if (!_active) return null;
            _active = false;

            var failures = _steps.Where(s => s.Status == "failed").ToList();
            var errorsPath = String.IsNullOrEmpty(_projectRoot)
                ? null
                : Path.Combine(_projectRoot, ErrorsFileName);

            if (!failures.Any())
            {
                // Clean build — a stale errors.json from a previous run must not survive,
                // or every consumer keeps reporting a failure that has since been fixed.
                if (errorsPath != null && File.Exists(errorsPath))
                {
                    try
                    {
                        File.Delete(errorsPath);
                        CliLog.LogLine($"build clean — removed stale {ErrorsFileName}");
                    }
                    catch (Exception ex)
                    {
                        CliLog.LogLine($"WARNING: could not remove stale {errorsPath}: {ex.Message}", ConsoleColor.Yellow);
                    }
                }
                return null;
            }

            var report = new
            {
                schema = SchemaId,
                generatedAt = DateTime.UtcNow.ToString("o"),
                startedAt = _startedAtUtc.ToString("o"),
                projectRoot = _projectRoot,
                buildCommand = _buildCommand,
                continueOnError = _continueOnError,
                totalSteps = _steps.Count,
                succeededSteps = _steps.Count(s => s.Status == "succeeded"),
                failedSteps = failures.Count,
                skippedSteps = _steps.Count(s => s.Status == "skipped"),
                failedStepNames = failures.Select(f => f.Name).ToList(),
                // `steps` is the lightweight index of what the build actually did, in order
                // — deliberately WITHOUT the exception payloads, so it stays readable. The
                // full stack traces live once, in `errors`, rather than twice in one file.
                steps = _steps.Select(s => new
                {
                    name = s.Name,
                    relativePath = s.RelativePath,
                    status = s.Status,
                    message = s.Message,
                    finishedAt = s.FinishedAt,
                }).ToList(),
                errors = failures,
            };

            PrintSummary(failures, errorsPath);

            if (errorsPath == null)
            {
                CliLog.LogLine($"(no project root resolved — {ErrorsFileName} was not written)", ConsoleColor.Yellow);
                return null;
            }

            try
            {
                File.WriteAllText(errorsPath, JsonConvert.SerializeObject(report, CamelCase));
                return errorsPath;
            }
            catch (Exception ex)
            {
                CliLog.LogLine($"WARNING: could not write {errorsPath}: {ex.Message}", ConsoleColor.Yellow);
                return null;
            }
        }

        private static void PrintSummary(List<StepRecord> failures, string errorsPath)
        {
            var succeeded = _steps.Count(s => s.Status == "succeeded");
            Console.WriteLine();
            CliLog.LogLine("============================================================", ConsoleColor.Yellow);
            CliLog.LogLine(
                _continueOnError
                    ? $"BUILD FINISHED WITH {failures.Count} FAILED STEP(S) — {succeeded} step(s) succeeded and are usable."
                    : $"BUILD FAILED — {failures.Count} failed step(s), {succeeded} succeeded.",
                ConsoleColor.Yellow);
            CliLog.LogLine("============================================================", ConsoleColor.Yellow);
            foreach (var f in failures)
            {
                CliLog.LogLine($"  FAILED: {f.Name}  ({f.RelativePath})", ConsoleColor.Red);
                CliLog.LogLine($"    command: effortless {f.CommandLine}", ConsoleColor.Red);
                CliLog.LogLine($"    error:   {f.Message}", ConsoleColor.Red);
            }
            if (!String.IsNullOrEmpty(errorsPath))
                CliLog.LogLine($"Full exception detail (stack traces, inner exceptions): {errorsPath}", ConsoleColor.Cyan);
            if (_continueOnError)
                CliLog.LogLine("-continueOnError is set, so the build ran every remaining step and is reporting success.", ConsoleColor.Cyan);
            Console.WriteLine();
        }

        private static string FirstNonBlank(params string[] candidates)
        {
            foreach (var c in candidates)
                if (!String.IsNullOrWhiteSpace(c)) return c;
            return null;
        }

        // Walk the whole InnerException chain — a tool's real cause is very often two or
        // three levels down, and the console output only ever shows the outermost message.
        private static ExceptionRecord Describe(Exception ex)
        {
            if (ReferenceEquals(ex, null)) return null;
            return new ExceptionRecord
            {
                Type = ex.GetType().FullName,
                Message = String.IsNullOrWhiteSpace(ex.Message) ? "(the exception carried an empty message)" : ex.Message,
                StackTrace = ex.StackTrace,
                Inner = Describe(ex.InnerException),
            };
        }

        public class StepRecord
        {
            public string Name { get; set; }
            public string RelativePath { get; set; }
            public string CommandLine { get; set; }
            public string Status { get; set; }
            public int? ExitCode { get; set; }
            public string Message { get; set; }
            public string ResolvedVersion { get; set; }
            public string ResolvedUrl { get; set; }
            public string FinishedAt { get; set; }
            public ExceptionRecord TranspilerException { get; set; }
            public ExceptionRecord CliException { get; set; }
        }

        public class ExceptionRecord
        {
            public string Type { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public ExceptionRecord Inner { get; set; }
        }
    }
}
