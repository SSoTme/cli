using Effortless.Cli;
using Effortless.Cli.Options;
using Effortless.Cli.Project;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Sockets;

namespace Effortless.Cli.Tests;

public sealed class RemoteToolsIndexTests
{
    private const string Head = "v2026.01.01.0001";
    private const string Old = "v2025.12.31.2359";

    [Fact(DisplayName = "unit-remote-index-resolve: catalog names, versions, and pins resolve deterministically")]
    public void ResolvesShortCanonicalExplicitAndPinnedVersions()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        WriteCatalog(index, Catalog());

        var latest = index.Resolve("to-uppercase");
        var canonical = index.Resolve(
            "effortless/common/to-uppercase");
        var explicitOld = index.Resolve($"to-uppercase/{Old}");
        var pinnedOld = index.Resolve("to-uppercase", Old);
        var pinnedHead = index.Resolve("to-uppercase", Head);

        Assert.Equal(Head, latest.VersionKey);
        Assert.Equal(
            $"effortless/common/to-uppercase {Head} [latest]",
            latest.Label);
        Assert.Equal(
            "effortless/common/to-uppercase",
            canonical.ToolName);
        Assert.Equal(Old, explicitOld.VersionKey);
        Assert.Equal(
            $"effortless/common/to-uppercase {Old}",
            explicitOld.Label);
        Assert.Equal(
            $"effortless/common/to-uppercase {Old} [pinned]",
            pinnedOld.Label);
        Assert.Equal(
            $"effortless/common/to-uppercase {Head} [pinned, latest]",
            pinnedHead.Label);
    }

    [Fact]
    public void LatestIgnoresHardPinButExplicitVersionStillWins()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        WriteCatalog(index, Catalog());

        var latest = index.Resolve(
            "to-uppercase",
            Old,
            latest: true);
        var explicitOld = index.Resolve(
            $"to-uppercase/{Old}",
            Head,
            latest: true);

        Assert.Equal(Head, latest.VersionKey);
        Assert.EndsWith("[latest]", latest.Label);
        Assert.Equal(Old, explicitOld.VersionKey);
        Assert.DoesNotContain("[pinned", explicitOld.Label);
    }

    [Fact]
    public void AmbiguousShortNamePrefersEffortlessAccount()
    {
        using var directory = new TestDirectory();
        var messages = new List<string>();
        var index = CreateIndex(directory, messages.Add);
        WriteCatalog(
            index,
            new JObject
            {
                ["transpilerVersions"] = new JObject
                {
                    ["acme/x/echo"] = Versions("acme"),
                    ["effortless/y/echo"] = Versions("effortless"),
                },
            });

        var resolution = index.Resolve("echo");

        Assert.Equal("effortless/y/echo", resolution.ToolName);
        Assert.Contains(
            "Warning: 'echo' matched multiple tools: acme/x/echo, effortless/y/echo. Using 'effortless/y/echo'. Provide a fully-qualified name to suppress this warning.",
            messages);
    }

    [Fact]
    public void SpecificVersionAndNoHeadErrorsDoNotBecomeMissingTools()
    {
        using var directory = new TestDirectory();
        var messages = new List<string>();
        var index = CreateIndex(directory, messages.Add);
        var catalog = Catalog();
        var toolMap = Assert.IsType<JObject>(
            catalog["transpilerVersions"]);
        var versions = Assert.IsType<JObject>(
            toolMap["effortless/common/to-uppercase"]);
        foreach (var version in versions.Properties())
        {
            var metadata = Assert.IsType<JObject>(
                version.Value["metaData"]);
            metadata["isHeadVersion"] = false;
        }
        WriteCatalog(index, catalog);

        var missingVersion = index.Resolve("to-uppercase/v7");
        var noHead = index.Resolve("to-uppercase");

        Assert.True(missingVersion.HasSpecificError);
        Assert.True(missingVersion.HasExplicitVersionError);
        Assert.True(noHead.HasSpecificError);
        Assert.False(noHead.HasExplicitVersionError);
        Assert.Contains(
            "Error: version 'v7' not found for tool 'effortless/common/to-uppercase'. Available versions: v2026.01.01.0001, v2025.12.31.2359",
            messages);
        Assert.Contains(
            "Error: this tool has no head versions; please specify a version to run via ssotme to-uppercase/version. use ssotme to-uppercase -list to view all available versions",
            messages);
    }

    [Fact]
    public void EmptyIndexRefreshesOnceAndProcessesBridgeMetadata()
    {
        using var directory = new TestDirectory();
        var calls = 0;
        RemoteToolsIndex? index = null;
        index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            cliVersion: "2026-08-30.23.43",
            refreshRunner: request =>
            {
                calls++;
                var root = Catalog();
                root["cliUpdateAvailable"] = new JObject
                {
                    ["name"] = "2026-09-01.00.00",
                };
                root["latestBridgeVersion"] = new JObject
                {
                    ["url"] = "https://bridge.example.test/v2",
                    ["versionIndex"] = 12,
                };
                WriteCatalog(index!, root);
                return true;
            },
            validateHost: _ => { });

        Assert.True(index.EnsureFresh());
        var first = index.Resolve("to-uppercase");
        var missing = index.Resolve("not-present");

        Assert.Equal(Head, first.VersionKey);
        Assert.Null(missing);
        Assert.Equal(1, calls);
        Assert.Equal(
            "2026-08-30.23.43",
            File.ReadAllText(index.CliVersionFile.FullName));
        Assert.Equal(
            "12",
            File.ReadAllText(index.BridgeVersionIndexFile.FullName));
        Assert.Equal(
            "https://bridge.example.test/v2/",
            index.TryGetToolUrl(RemoteToolsIndex.BridgeToolName));
        Assert.True(File.Exists(index.UpdateAvailableFile.FullName));
        Assert.True(File.Exists(index.ProjectFile.FullName));
    }

    [Fact]
    public void ListVersionsUsesVersionOrderAndHeadMetadata()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        WriteCatalog(index, Catalog());

        var list = index.ListVersions("to-uppercase");

        Assert.Equal(
            "effortless/common/to-uppercase",
            list.ToolName);
        Assert.Equal([Head, Old], list.Versions.Select(x => x.VersionKey));
        Assert.True(list.Versions[0].IsHead);
        Assert.False(list.Versions[1].IsHead);
    }

    [Fact]
    public void DeadStoredBridgeResetsToBootstrapAndRetriesOnce()
    {
        using var directory = new TestDirectory();
        RemoteToolsIndex? index = null;
        var bridgeRuns = new List<string>();
        index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            cliVersion: "test",
            refreshRunner: request =>
            {
                bridgeRuns.Add(request.BridgeUrl);
                WriteCatalog(index!, Catalog());
                return true;
            },
            writeLine: _ => { },
            validateHost: host =>
            {
                if (string.Equals(
                        host,
                        "dead.example.test",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new HttpRequestException(
                        "No such host is known",
                        new SocketException(
                            (int)SocketError.HostNotFound));
                }
            });
        Directory.CreateDirectory(index.ConfigRoot.FullName);
        File.WriteAllText(
            index.ToolUrlsFile.FullName,
            """
            {
              "cli-cloud-bridge": "https://dead.example.test/"
            }
            """);
        File.WriteAllText(index.BridgeVersionIndexFile.FullName, "99");

        var refreshed = index.Refresh("test refresh", "test dead host");

        Assert.True(refreshed);
        Assert.Equal([RemoteToolsIndex.BootstrapBridgeUrl], bridgeRuns);
        Assert.Equal(
            RemoteToolsIndex.BootstrapBridgeUrl,
            index.TryGetToolUrl(RemoteToolsIndex.BridgeToolName));
        Assert.False(File.Exists(index.BridgeVersionIndexFile.FullName));
    }

    [Theory]
    [InlineData("{ malformed")]
    [InlineData("""{"transpilerVersions":{}}""")]
    [InlineData("""{"notAToolMap":{}}""")]
    public void InvalidRefreshPreservesPriorCatalogBytes(
        string replacement)
    {
        using var directory = new TestDirectory();
        RemoteToolsIndex? index = null;
        index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            refreshRunner: _ =>
            {
                File.WriteAllText(
                    index!.IndexFile.FullName,
                    replacement);
                return true;
            },
            writeLine: _ => { },
            validateHost: _ => { });
        WriteCatalog(index, Catalog());
        var before = File.ReadAllBytes(
            index.IndexFile.FullName);

        var refreshed = index.Refresh(
            "test refresh",
            "test invalid replacement");

        Assert.False(refreshed);
        Assert.Equal(
            before,
            File.ReadAllBytes(index.IndexFile.FullName));
    }

    [Fact]
    public void ToolResolverAppliesDirectUrlsWithoutCatalogLookup()
    {
        using var directory = new TestDirectory();
        var calls = 0;
        var index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            refreshRunner: _ =>
            {
                calls++;
                return false;
            },
            writeLine: _ => { },
            validateHost: _ => { });
        var invocation = new CliInvocation
        {
            Options = new CliOptions
            {
                targetUrl = "https://tools.example.test/direct/",
            },
            RemainingArguments = ["ignored-tool"],
        };

        new ToolResolver(index).Resolve(invocation);

        Assert.Equal(
            "https://tools.example.test/direct/",
            invocation.TargetUrl);
        Assert.Equal(
            "httpstoolsexampletestdirect",
            invocation.Transpiler);
        Assert.Equal(0, calls);
        Assert.Null(invocation.ResolvedVersionLabel);
    }

    [Fact]
    public void ToolResolverPreservesRemoteMetadataUnderLocalOverride()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        WriteCatalog(index, Catalog());
        File.WriteAllText(
            index.ToolUrlsFile.FullName,
            """
            {
              "to-uppercase": "http://localhost:43210/local/"
            }
            """);
        var invocation = new CliInvocation
        {
            Options = new CliOptions(),
            RemainingArguments = ["to-uppercase"],
        };

        new ToolResolver(index).Resolve(invocation);

        Assert.Equal(
            "http://localhost:43210/local/",
            invocation.TargetUrl);
        Assert.Equal(
            "to-uppercase [user-set]",
            invocation.ResolvedVersionLabel);
        Assert.Equal(Head, invocation.ResolvedVersionKey);
        Assert.Equal(
            "effortless/common/to-uppercase",
            invocation.ResolvedToolName);
        Assert.Contains(
            "uppercase-head",
            invocation.ResolvedVersionUrl);
    }

    [Fact]
    public void ToolResolverUsesHardPinOnlyWhenProjectIsAlreadyLoaded()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        WriteCatalog(index, Catalog());
        var project = new EffortlessProject
        {
            RootPath = directory.Path,
        };
        project.ProjectTranspilers.Add(
            new ProjectTranspiler
            {
                CommandLine = "to-uppercase -i input.txt",
                RelativePath = string.Empty,
                PinnedVersion = Old,
            });
        var invocation = new CliInvocation
        {
            Options = new CliOptions(),
            RemainingArguments = ["to-uppercase"],
            Project = project,
            CurrentDirectory = directory.Path,
        };

        new ToolResolver(index).Resolve(invocation);

        Assert.Equal(Old, invocation.ResolvedVersionKey);
        Assert.EndsWith("[pinned]", invocation.ResolvedVersionLabel);
    }

    [Fact]
    public void ToolUrlsOnlyToolAvoidsRefreshAndMissingToolStaysUnresolved()
    {
        using var directory = new TestDirectory();
        var calls = 0;
        var index = new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            refreshRunner: _ =>
            {
                calls++;
                return false;
            },
            writeLine: _ => { },
            validateHost: _ => { });
        index.EnsureInitialized();
        File.WriteAllText(
            index.ToolUrlsFile.FullName,
            """
            {
              "my-tool": "http://localhost:43210/my-tool/"
            }
            """);
        var local = new CliInvocation
        {
            Options = new CliOptions(),
            RemainingArguments = ["my-tool"],
        };

        new ToolResolver(index).Resolve(local);

        Assert.Equal(
            "http://localhost:43210/my-tool/",
            local.TargetUrl);
        Assert.Equal(0, calls);

        var missing = new CliInvocation
        {
            Options = new CliOptions(),
            RemainingArguments = ["missing-tool"],
        };
        new ToolResolver(index).Resolve(missing);

        Assert.Null(missing.TargetUrl);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void CorruptToolUrlConfigurationFailsExplicitly()
    {
        using var directory = new TestDirectory();
        var index = CreateIndex(directory);
        index.EnsureInitialized();
        File.WriteAllText(index.ToolUrlsFile.FullName, "{ invalid");

        var error = Assert.Throws<InvalidDataException>(
            () => index.TryGetToolUrl("to-uppercase"));

        Assert.Contains(
            "Error reading ~/.ssotme/tool_urls.json",
            error.Message,
            StringComparison.Ordinal);
    }

    private static RemoteToolsIndex CreateIndex(
        TestDirectory directory,
        Action<string>? writeLine = null)
    {
        return new RemoteToolsIndex(
            new DirectoryInfo(directory.Path),
            cliVersion: "test",
            writeLine: writeLine ?? (_ => { }),
            validateHost: _ => { });
    }

    private static void WriteCatalog(
        RemoteToolsIndex index,
        JObject root)
    {
        Directory.CreateDirectory(index.RemoteToolsDirectory.FullName);
        File.WriteAllText(index.IndexFile.FullName, root.ToString());
    }

    private static JObject Catalog()
    {
        return new JObject
        {
            ["transpilerVersions"] = new JObject
            {
                ["effortless/common/to-uppercase"] = new JObject
                {
                    [Head] = Version("uppercase-head", isHead: true),
                    [Old] = Version("uppercase-old", isHead: false),
                },
            },
            ["cliUpdateAvailable"] = false,
            ["latestBridgeVersion"] = null,
            ["fetchedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private static JObject Versions(string path)
    {
        return new JObject
        {
            [Head] = Version(path, isHead: true),
        };
    }

    private static JObject Version(string path, bool isHead)
    {
        return new JObject
        {
            ["metaData"] = new JObject
            {
                ["isHeadVersion"] = isHead,
            },
            ["urls"] = new JObject
            {
                ["post"] = $"https://tools.example.test/{path}/",
            },
        };
    }
}
