#nullable enable
using System.Runtime.InteropServices;
using Effortless.Cli.LocalTools;
using Effortless.Cli.Options;

namespace Effortless.Cli.Commands;

/// <summary>
/// <c>effortless serve [-port N]</c>: runs the local transpiler host resident
/// for the current project (step 12). Builds in the project reuse it through
/// <c>.effortless/serve.json</c> instead of starting an ephemeral host.
/// </summary>
public sealed class ServeCommand
{
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Test seam: when set, serve exits on its own after this many
    /// milliseconds instead of waiting for Ctrl+C.
    /// </summary>
    public const string AutoExitEnvVariable = "EFFORTLESS_SERVE_EXIT_AFTER_MS";

    public int Run(CliInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var project = invocation.Project
            ?? throw new InvalidOperationException("serve needs a loaded project.");
        var root = Path.GetFullPath(project.RootPath);
        var catalog = LocalToolCatalog.Discover(root);
        ReportProblems(catalog);
        if (catalog.IsEmpty)
        {
            WriteColor(
                $"No local tools found under {catalog.ToolsDirectory}.",
                ConsoleColor.Yellow);
            return 0;
        }

        using var host = new LocalToolHost(
            catalog,
            debug: invocation.Options.debug ? Console.WriteLine : null,
            requestLog: Console.WriteLine);
        host.Start(invocation.Options.port);
        ServeState.Write(root, host.Port);
        PrintTools(host);
        Console.WriteLine("Press Ctrl+C to stop.");

        using var stopping = new CancellationTokenSource();
        using var watcher = CreateWatcher(host, stopping.Token);
        using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            stopping.Cancel();
        });
        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stopping.Cancel();
        });

        var autoExit = Environment.GetEnvironmentVariable(AutoExitEnvVariable);
        if (int.TryParse(autoExit, out var autoExitMs) && autoExitMs > 0)
        {
            stopping.CancelAfter(autoExitMs);
        }

        try
        {
            stopping.Token.WaitHandle.WaitOne();
        }
        finally
        {
            ServeState.Delete(root);
            host.Dispose();
            Console.WriteLine("[cli] Local tool host stopped.");
        }

        return 0;
    }

    private static void PrintTools(LocalToolHost host)
    {
        var catalog = host.Catalog;
        Console.WriteLine(
            $"[cli] Local tool host listening on {host.BaseUri} ({catalog.Tools.Count} tool(s))");
        foreach (var tool in catalog.Tools)
        {
            Console.WriteLine($"  {tool.Name}  {host.ToolUri(tool.Name)}  ({tool.RuntimeName})");
        }
    }

    private static void ReportProblems(LocalToolCatalog catalog)
    {
        foreach (var problem in catalog.Problems)
        {
            WriteColor(problem.Message, ConsoleColor.Red);
        }
    }

    private static FileSystemWatcher? CreateWatcher(LocalToolHost host, CancellationToken stopping)
    {
        var toolsDirectory = host.Catalog.ToolsDirectory;
        if (!Directory.Exists(toolsDirectory))
        {
            return null;
        }

        var watcher = new FileSystemWatcher(toolsDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size,
        };
        var gate = new object();
        CancellationTokenSource? pending = null;
        void Schedule()
        {
            lock (gate)
            {
                pending?.Cancel();
                pending = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                var token = pending.Token;
                _ = Task.Delay(ReloadDebounce, token).ContinueWith(
                    _ =>
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        var reloaded = LocalToolCatalog.Discover(host.Catalog.ProjectRoot);
                        host.Reload(reloaded);
                        ReportProblems(reloaded);
                        Console.WriteLine(
                            $"[cli] effortless-tools/ changed; {reloaded.Tools.Count} tool(s) now served.");
                        PrintTools(host);
                    },
                    TaskScheduler.Default);
            }
        }

        watcher.Changed += (_, _) => Schedule();
        watcher.Created += (_, _) => Schedule();
        watcher.Deleted += (_, _) => Schedule();
        watcher.Renamed += (_, _) => Schedule();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private static void WriteColor(string message, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
