using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Effortless.Cli.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Effortless.Cli;

/// <summary>
/// Reads and refreshes the versioned tool catalog stored under
/// <c>~/.effortless/remote_tools</c>.
/// </summary>
public sealed class RemoteToolsIndex
{
    public const string BridgeToolName = "cli-cloud-bridge";
    public const string BootstrapBridgeUrl =
        "https://ssotme-cli-cloud-bridge-v2026-04-24-1853-cmvbd4phczmeg.7pktzg2z971j0.cpln.app";

    private readonly Action<string> _writeLine;
    private readonly Func<RemoteToolsRefreshRequest, bool> _refreshRunner;
    private readonly Action<string> _validateHost;
    private readonly Func<string, string> _toolUrlLookup;
    private readonly Action<string, string> _toolUrlSetter;
    private readonly Action<string> _toolUrlRemover;
    private readonly Func<string> _commandLine;
    private readonly CatalogFreshnessPolicy _freshnessPolicy;
    private readonly object _loadLock = new();
    private JObject _rawRoot;

    /// <summary>
    /// Creates an index rooted in the current user's standard configuration directory.
    /// </summary>
    public RemoteToolsIndex(
        string cliVersion = null,
        Func<RemoteToolsRefreshRequest, bool> refreshRunner = null,
        Action<string> writeLine = null,
        Action<string> validateHost = null,
        Func<string> commandLine = null,
        TimeProvider timeProvider = null)
        : this(
            UserConfigDir.EffortlessDir,
            cliVersion,
            refreshRunner,
            writeLine,
            validateHost,
            commandLine,
            timeProvider,
            useSharedToolUrls: true)
    {
    }

    /// <summary>
    /// Creates an index under an explicit configuration root. This overload is useful
    /// for hosts and tests that isolate user configuration.
    /// </summary>
    public RemoteToolsIndex(
        DirectoryInfo configRoot,
        string cliVersion = null,
        Func<RemoteToolsRefreshRequest, bool> refreshRunner = null,
        Action<string> writeLine = null,
        Action<string> validateHost = null,
        Func<string> commandLine = null,
        TimeProvider timeProvider = null)
        : this(
            configRoot,
            cliVersion,
            refreshRunner,
            writeLine,
            validateHost,
            commandLine,
            timeProvider,
            useSharedToolUrls: false)
    {
    }

    private RemoteToolsIndex(
        DirectoryInfo configRoot,
        string cliVersion,
        Func<RemoteToolsRefreshRequest, bool> refreshRunner,
        Action<string> writeLine,
        Action<string> validateHost,
        Func<string> commandLine,
        TimeProvider timeProvider,
        bool useSharedToolUrls)
    {
        ArgumentNullException.ThrowIfNull(configRoot);

        ConfigRoot = configRoot;
        RemoteToolsDirectory = new DirectoryInfo(
            Path.Combine(configRoot.FullName, "remote_tools"));
        IndexFile = new FileInfo(
            Path.Combine(RemoteToolsDirectory.FullName, "effortless-tools.json"));
        CliVersionFile = new FileInfo(
            Path.Combine(RemoteToolsDirectory.FullName, "cli_version"));
        ProjectFile = new FileInfo(
            Path.Combine(RemoteToolsDirectory.FullName, "effortless.json"));
        ToolUrlsFile = new FileInfo(
            Path.Combine(configRoot.FullName, "tool_urls.json"));
        BridgeVersionIndexFile = new FileInfo(
            Path.Combine(configRoot.FullName, "bridge_version_index"));
        UpdateAvailableFile = new FileInfo(
            Path.Combine(configRoot.FullName, "update_available.json"));

        CliVersion = string.IsNullOrWhiteSpace(cliVersion)
            ? global::Effortless.Cli.CliVersion.Value
            : cliVersion;
        _writeLine = writeLine ?? Console.WriteLine;
        _refreshRunner = refreshRunner;
        _validateHost = validateHost ?? ValidateHost;
        _commandLine = commandLine ?? (() => Environment.CommandLine);
        _freshnessPolicy = new CatalogFreshnessPolicy(timeProvider);

        if (useSharedToolUrls)
        {
            _toolUrlLookup = ToolUrls.TryGetUrlFromFileUrls;
            _toolUrlSetter = ToolUrls.SetToolUrl;
            _toolUrlRemover = ToolUrls.RemoveToolUrl;
        }
        else
        {
            _toolUrlLookup = ReadToolUrl;
            _toolUrlSetter = WriteToolUrl;
            _toolUrlRemover = RemoveToolUrl;
        }
    }

    public DirectoryInfo ConfigRoot { get; }

    public DirectoryInfo RemoteToolsDirectory { get; }

    public FileInfo IndexFile { get; }

    public FileInfo CliVersionFile { get; }

    public FileInfo ProjectFile { get; }

    public FileInfo ToolUrlsFile { get; }

    public FileInfo BridgeVersionIndexFile { get; }

    public FileInfo UpdateAvailableFile { get; }

    public string CliVersion { get; }

    /// <summary>
    /// The last successfully parsed catalog root. The returned object is the raw
    /// bridge response so bridge side-effect consumers can inspect extension fields.
    /// </summary>
    public JObject RawRoot
    {
        get
        {
            Load();
            return _rawRoot;
        }
    }

    /// <summary>
    /// True when no structurally valid tool entries can be read.
    /// A malformed index is deliberately treated as empty.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            var tools = Load();
            return tools == null || !tools.Properties().Any();
        }
    }

    /// <summary>
    /// True when the index exists but is malformed or lacks a supported tool map.
    /// </summary>
    public bool IsCorrupt { get; private set; }

    public string LastRefreshError { get; private set; }

    /// <summary>
    /// Proves that the local catalog was validated within the preceding 24 hours.
    /// A failed refresh is fatal; stale bytes are never used as a fallback.
    /// </summary>
    public bool EnsureFresh()
    {
        EnsureInitialized();
        var tools = Load();
        if (tools != null
            && tools.Properties().Any()
            && _freshnessPolicy.IsFresh(_rawRoot))
        {
            return true;
        }

        _writeLine("[cli] Refreshing CLI tool URL index...");
        return Refresh(
            "Remote tools catalog freshness required",
            "The cached remote tools index is missing, invalid, empty, or at least 24 hours old. A validated replacement is required before catalog-dependent work can continue.");
    }

    /// <summary>
    /// Creates the index directory and an empty placeholder without contacting the
    /// bridge. Catalog population is performed only by <see cref="Refresh"/>.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!ConfigRoot.Exists)
        {
            ConfigRoot.Create();
        }

        if (!RemoteToolsDirectory.Exists)
        {
            RemoteToolsDirectory.Create();
        }

        IndexFile.Refresh();
        if (!IndexFile.Exists)
        {
            File.WriteAllText(IndexFile.FullName, "{\"transpilers\":{}}");
            _writeLine("[cli] Initializing CLI tool URL index...");
        }

        if (string.IsNullOrWhiteSpace(TryGetToolUrl(BridgeToolName)))
        {
            SetToolUrl(BridgeToolName, BootstrapBridgeUrl);
        }
    }

    /// <summary>
    /// Resolves a canonical or short tool name. An explicit version suffix wins over
    /// a hard pin; <paramref name="latest"/> ignores a hard pin and selects HEAD.
    /// The caller must establish catalog freshness before invoking this method.
    /// </summary>
    public RemoteToolResolution Resolve(
        string name,
        string pinnedVersion = null,
        bool latest = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var resolution = ResolveFromCurrentRoot(name, pinnedVersion, latest);
        if (resolution != null)
        {
            return resolution;
        }

        // Step 3 preserves the characterized legacy rule: a non-empty catalog
        // miss is definitive for this invocation. Step 03A introduces the
        // catalog-freshness/miss refresh gate.
        return null;
    }

    /// <summary>
    /// Resolves the current HEAD while ignoring both command-line version suffixes
    /// and project hard pins.
    /// </summary>
    public RemoteToolResolution ResolveHead(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        SplitVersionSuffix(name, out var toolPart, out _);
        return ResolveFromCurrentRoot(
            toolPart,
            pinnedVersion: null,
            latest: true);
    }

    public IReadOnlyList<RemoteCatalogTool> ListTools(string search = null)
    {
        var tools = Load();
        if (tools == null)
        {
            return Array.Empty<RemoteCatalogTool>();
        }

        return tools.Properties()
            .Select(property =>
            {
                var versions = property.Value as JObject;
                var head = versions?.Properties()
                    .FirstOrDefault(version =>
                        version.Value["metaData"]?["isHeadVersion"]
                            ?.Value<bool>() == true);
                return new RemoteCatalogTool(
                    property.Name,
                    property.Name.Split('/').Last(),
                    head?.Name);
            })
            .Where(tool =>
                string.IsNullOrEmpty(search)
                || tool.CanonicalName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                || tool.ShortName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                tool => tool.CanonicalName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                tool => tool.CanonicalName,
                StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Returns all versions for a tool in descending version order.
    /// </summary>
    public RemoteToolVersionList ListVersions(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var tools = Load();
        if (tools == null)
        {
            return null;
        }

        SplitVersionSuffix(name, out var toolPart, out _);
        var candidate = SelectCandidate(tools, toolPart);
        if (candidate == null)
        {
            return null;
        }

        var versions = tools[candidate] as JObject;
        if (versions == null)
        {
            return new RemoteToolVersionList(
                candidate,
                Array.Empty<RemoteToolVersion>());
        }

        var entries = versions.Properties()
            .Select(property => new RemoteToolVersion(
                property.Name,
                property.Value["urls"]?["post"]?.Value<string>(),
                property.Value["metaData"]?["isHeadVersion"]?.Value<bool>()
                    == true))
            .OrderByDescending(entry => VersionKey.ParseVersionKey(entry.VersionKey))
            .ThenByDescending(entry => entry.VersionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RemoteToolVersionList(candidate, entries);
    }

    /// <summary>
    /// Tests the current cache for a canonical or short tool name without refreshing
    /// it or emitting ambiguity diagnostics.
    /// </summary>
    public bool ContainsTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var tools = Load();
        if (tools == null)
        {
            return false;
        }

        SplitVersionSuffix(name, out var toolPart, out _);
        var suffix = "/" + toolPart;
        return tools.Properties().Any(property =>
            string.Equals(
                property.Name,
                toolPart,
                StringComparison.OrdinalIgnoreCase)
            || property.Name.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Runs the injected bridge operation and applies bridge-owned side effects.
    /// No alternate transport or stale-catalog fallback is attempted on failure.
    /// </summary>
    public bool Refresh(string triggerTitle, string triggerDetails)
    {
        if (string.IsNullOrWhiteSpace(triggerTitle))
        {
            throw new ArgumentException(
                "A bridge refresh requires a non-empty trigger title.",
                nameof(triggerTitle));
        }

        if (string.IsNullOrWhiteSpace(triggerDetails))
        {
            throw new ArgumentException(
                "A bridge refresh requires non-empty trigger details.",
                nameof(triggerDetails));
        }

        EnsureInitialized();
        EnsureRefreshProject();
        RemoveLegacyBridgeMapping();
        PrintRefreshBanner(triggerTitle, triggerDetails);
        LastRefreshError = null;
        var previousBytes = ReadIndexBytes();

        if (_refreshRunner == null)
        {
            LastRefreshError =
                "No remote tools refresh runner has been configured.";
            return false;
        }

        var bridgeUrl = TryGetToolUrl(BridgeToolName);
        if (string.IsNullOrWhiteSpace(bridgeUrl))
        {
            bridgeUrl = BootstrapBridgeUrl;
            SetToolUrl(BridgeToolName, bridgeUrl);
        }

        if (!RunRefreshAttempt(bridgeUrl, out var error))
        {
            if (!IsHostNotFound(error)
                || SameUrl(bridgeUrl, BootstrapBridgeUrl))
            {
                RestoreIndex(previousBytes);
                LastRefreshError = error?.Message
                    ?? "The remote tools refresh failed.";
                return false;
            }

            _writeLine(
                $"Stored bridge URL is unreachable ({bridgeUrl}). Resetting to bootstrap URL and retrying...");
            SetToolUrl(BridgeToolName, BootstrapBridgeUrl);
            DeleteIfPresent(BridgeVersionIndexFile);

            if (!RunRefreshAttempt(BootstrapBridgeUrl, out error))
            {
                RestoreIndex(previousBytes);
                LastRefreshError = error?.Message
                    ?? "The remote tools refresh failed after resetting the bridge URL.";
                return false;
            }
        }

        var candidateBytes = ReadIndexBytes();
        if (!TryParseValidCatalog(
                candidateBytes,
                out var candidateRoot,
                out var validationError))
        {
            RestoreIndex(previousBytes);
            LastRefreshError = validationError;
            return false;
        }

        candidateRoot["fetchedAt"] =
            _freshnessPolicy.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);
        AtomicWriteIndex(candidateRoot);
        Load();
        File.WriteAllText(CliVersionFile.FullName, CliVersion);
        ProcessBridgeSideEffects(_rawRoot);
        return true;
    }

    /// <summary>
    /// Reads a custom URL from the shared <c>tool_urls.json</c>.
    /// </summary>
    public string TryGetToolUrl(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        return _toolUrlLookup(toolName);
    }

    private RemoteToolResolution ResolveFromCurrentRoot(
        string name,
        string pinnedVersion,
        bool latest)
    {
        var tools = Load();
        if (tools == null)
        {
            return null;
        }

        SplitVersionSuffix(
            name,
            out var toolPart,
            out var explicitVersion);
        var candidate = SelectCandidate(tools, toolPart);
        if (candidate == null)
        {
            return null;
        }

        var versions = tools[candidate] as JObject;
        if (versions == null)
        {
            return null;
        }

        var head = versions.Properties()
            .FirstOrDefault(property =>
                property.Value["metaData"]?["isHeadVersion"]?.Value<bool>()
                == true);
        var requestedVersion = explicitVersion;
        var hardPinActive = !latest
            && !string.IsNullOrWhiteSpace(pinnedVersion);
        if (requestedVersion == null && hardPinActive)
        {
            requestedVersion = pinnedVersion;
        }

        JProperty selected;
        if (requestedVersion != null)
        {
            selected = versions.Properties()
                .FirstOrDefault(property => string.Equals(
                    property.Name,
                    requestedVersion,
                    StringComparison.OrdinalIgnoreCase));
            if (selected == null)
            {
                var available = string.Join(
                    ", ",
                    versions.Properties().Select(property => property.Name));
                _writeLine(
                    $"Error: version '{requestedVersion}' not found for tool '{candidate}'. Available versions: {available}");
                return RemoteToolResolution.SpecificError(
                    candidate,
                    hasExplicitVersionError: true);
            }
        }
        else
        {
            selected = head;
            if (selected == null)
            {
                _writeLine(
                    $"Error: this tool has no head versions; please specify a version to run via effortless {toolPart}/version. use effortless {toolPart} -list to view all available versions");
                return RemoteToolResolution.SpecificError(
                    candidate,
                    hasExplicitVersionError: false);
            }
        }

        var isAtHead = head != null
            && string.Equals(
                selected.Name,
                head.Name,
                StringComparison.OrdinalIgnoreCase);
        var suffix = hardPinActive
            ? isAtHead ? " [pinned, latest]" : " [pinned]"
            : isAtHead ? " [latest]" : string.Empty;
        var url = selected.Value["urls"]?["post"]?.Value<string>();

        return new RemoteToolResolution(
            url,
            selected.Name,
            candidate,
            $"{candidate} {selected.Name}{suffix}",
            hasSpecificError: false,
            hasExplicitVersionError: false);
    }

    private JObject Load()
    {
        lock (_loadLock)
        {
            IsCorrupt = false;
            _rawRoot = null;

            IndexFile.Refresh();
            if (!IndexFile.Exists)
            {
                return null;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(IndexFile.FullName));
                var tools = (root["transpilerVersions"]
                             ?? root["transpilers"]) as JObject;
                if (tools == null)
                {
                    IsCorrupt = true;
                    return null;
                }

                _rawRoot = root;
                return tools;
            }
            catch (JsonException)
            {
                IsCorrupt = true;
                return null;
            }
            catch (IOException)
            {
                IsCorrupt = true;
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                IsCorrupt = true;
                return null;
            }
        }
    }

    private string SelectCandidate(JObject tools, string toolPart)
    {
        var suffix = "/" + toolPart;
        var candidates = tools.Properties()
            .Where(property =>
                string.Equals(
                    property.Name,
                    toolPart,
                    StringComparison.OrdinalIgnoreCase)
                || property.Name.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var best = candidates
            .Where(candidate => string.Equals(
                candidate.Split('/')[0],
                "effortless",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? candidates[0];
        _writeLine(
            $"Warning: '{toolPart}' matched multiple tools: {string.Join(", ", candidates)}. Using '{best}'. Provide a fully-qualified name to suppress this warning.");
        return best;
    }

    private bool RunRefreshAttempt(string bridgeUrl, out Exception error)
    {
        error = null;
        try
        {
            var uri = new Uri(bridgeUrl, UriKind.Absolute);
            _validateHost(uri.Host);
            var request = new RemoteToolsRefreshRequest(
                bridgeUrl,
                RemoteToolsDirectory.FullName,
                CliVersion,
                $"{BridgeToolName} -p cli_version={CliVersion}",
                IndexFile);
            if (!_refreshRunner(request))
            {
                error = new InvalidOperationException(
                    "The remote tools refresh runner reported failure.");
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }

    private byte[] ReadIndexBytes()
    {
        IndexFile.Refresh();
        if (!IndexFile.Exists)
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(IndexFile.FullName);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryParseValidCatalog(
        byte[] bytes,
        out JObject root,
        out string error)
    {
        root = null;
        error = null;
        if (bytes == null || bytes.Length == 0)
        {
            error = "The bridge produced an empty remote tools index.";
            return false;
        }

        try
        {
            root = JObject.Parse(
                System.Text.Encoding.UTF8.GetString(bytes));
        }
        catch (JsonException)
        {
            error = "The bridge produced a malformed remote tools index.";
            return false;
        }

        var tools = (root["transpilerVersions"]
                     ?? root["transpilers"]) as JObject;
        if (tools == null
            || !tools.Properties().Any()
            || tools.Properties().Any(
                property => property.Value is not JObject))
        {
            error =
                "The bridge produced an invalid or empty remote tools map.";
            return false;
        }

        return true;
    }

    private void AtomicWriteIndex(JObject root)
    {
        var tempPath = Path.Combine(
            RemoteToolsDirectory.FullName,
            $".{IndexFile.Name}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                tempPath,
                root.ToString(Formatting.Indented)
                + Environment.NewLine);
            File.Move(
                tempPath,
                IndexFile.FullName,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void RestoreIndex(byte[] previousBytes)
    {
        if (previousBytes == null)
        {
            DeleteIfPresent(IndexFile);
        }
        else
        {
            var tempPath = Path.Combine(
                RemoteToolsDirectory.FullName,
                $".{IndexFile.Name}.{Guid.NewGuid():N}.restore");
            try
            {
                File.WriteAllBytes(tempPath, previousBytes);
                File.Move(
                    tempPath,
                    IndexFile.FullName,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        lock (_loadLock)
        {
            _rawRoot = null;
        }
    }

    private void EnsureRefreshProject()
    {
        ProjectFile.Refresh();
        if (ProjectFile.Exists)
        {
            return;
        }

        var project = new JObject
        {
            ["ShowHidden"] = false,
            ["ShowAllFiles"] = false,
            ["CurrentPath"] = null,
            ["SSoTmeProjectFiles"] = null,
            ["Name"] = "remote_tools",
            ["ProjectSettings"] = new JArray
            {
                new JObject
                {
                    ["ProjectSettingId"] = Guid.NewGuid().ToString(),
                    ["Name"] = "project-name",
                    ["Value"] = "remote_tools",
                },
            },
            ["ProjectTranspilers"] = new JArray(),
        };
        File.WriteAllText(
            ProjectFile.FullName,
            project.ToString(Formatting.Indented) + Environment.NewLine);
    }

    private void ProcessBridgeSideEffects(JObject root)
    {
        var updateAvailable = root?["cliUpdateAvailable"] as JObject;
        if (!string.IsNullOrWhiteSpace(
                updateAvailable?["name"]?.Value<string>()))
        {
            File.WriteAllText(
                UpdateAvailableFile.FullName,
                updateAvailable.ToString(Formatting.Indented)
                + Environment.NewLine);
        }
        else
        {
            DeleteIfPresent(UpdateAvailableFile);
        }

        var bridge = root?["latestBridgeVersion"] as JObject;
        var newUrl = bridge?["url"]?.Value<string>();
        var newIndex = bridge?["versionIndex"]?.Value<long?>();
        if (string.IsNullOrWhiteSpace(newUrl) || newIndex == null)
        {
            return;
        }

        var currentIndex = -1L;
        BridgeVersionIndexFile.Refresh();
        if (BridgeVersionIndexFile.Exists)
        {
            long.TryParse(
                File.ReadAllText(BridgeVersionIndexFile.FullName).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out currentIndex);
        }

        if (newIndex.Value <= currentIndex)
        {
            return;
        }

        SetToolUrl(BridgeToolName, newUrl.TrimEnd('/') + "/");
        File.WriteAllText(
            BridgeVersionIndexFile.FullName,
            newIndex.Value.ToString(CultureInfo.InvariantCulture));
    }

    private void PrintRefreshBanner(
        string triggerTitle,
        string triggerDetails)
    {
        CliVersionFile.Refresh();
        var cachedVersion = CliVersionFile.Exists
            ? File.ReadAllText(CliVersionFile.FullName).Trim()
            : null;
        _writeLine(
            "================================================================================");
        _writeLine($"  CLOUD-BRIDGE CALL TRIGGERED: {triggerTitle}");
        _writeLine(
            "================================================================================");
        _writeLine($"  WHY:        {triggerDetails}");
        _writeLine(
            $"  CALLED BY:  {nameof(RemoteToolsIndex)}.{nameof(Refresh)}");
        _writeLine(
            $"  CLI VER:    current={CliVersion}, cached={cachedVersion ?? "(none)"}");
        _writeLine($"  CWD:        {Environment.CurrentDirectory}");
        _writeLine($"  TIME:       {_freshnessPolicy.UtcNow:O}");
        _writeLine($"  COMMAND:    {_commandLine()}");
        _writeLine(
            "================================================================================");
    }

    private void RemoveLegacyBridgeMapping()
    {
        try
        {
            _toolUrlRemover("list-transpilers");
        }
        catch
        {
            // Absence is the desired state.
        }
    }

    private void SetToolUrl(string toolName, string url)
    {
        _toolUrlSetter(toolName, url);
    }

    private string ReadToolUrl(string toolName)
    {
        try
        {
            return ReadToolUrls().TryGetValue(toolName, out var url)
                ? url
                : null;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Error reading ~/.effortless/tool_urls.json: {exception.Message}",
                exception);
        }
    }

    private void WriteToolUrl(string toolName, string url)
    {
        var mappings = ReadToolUrls();
        mappings[toolName] = url;
        WriteToolUrls(mappings);
    }

    private void RemoveToolUrl(string toolName)
    {
        var mappings = ReadToolUrls();
        if (mappings.Remove(toolName))
        {
            WriteToolUrls(mappings);
        }
    }

    private Dictionary<string, string> ReadToolUrls()
    {
        ToolUrlsFile.Refresh();
        if (!ToolUrlsFile.Exists)
        {
            return new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
        }

        var mappings =
            JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText(ToolUrlsFile.FullName))
            ?? new Dictionary<string, string>();
        return new Dictionary<string, string>(
            mappings,
            StringComparer.OrdinalIgnoreCase);
    }

    private void WriteToolUrls(Dictionary<string, string> mappings)
    {
        if (!ConfigRoot.Exists)
        {
            ConfigRoot.Create();
        }

        File.WriteAllText(
            ToolUrlsFile.FullName,
            JsonConvert.SerializeObject(mappings, Formatting.Indented)
            + Environment.NewLine);
    }

    private static void SplitVersionSuffix(
        string name,
        out string toolPart,
        out string versionPart)
    {
        toolPart = name;
        versionPart = null;
        var slash = name.LastIndexOf('/');
        if (slash < 0 || slash == name.Length - 1)
        {
            return;
        }

        var lastSegment = name.Substring(slash + 1);
        if (lastSegment.Length > 1
            && (lastSegment[0] == 'v' || lastSegment[0] == 'V')
            && char.IsDigit(lastSegment[1]))
        {
            toolPart = name.Substring(0, slash);
            versionPart = lastSegment;
        }
    }

    private static void ValidateHost(string host)
    {
        try
        {
            Dns.GetHostEntry(host);
        }
        catch (SocketException exception)
            when (exception.SocketErrorCode == SocketError.HostNotFound)
        {
            throw new HttpRequestException(
                $"No such host is known ({host})",
                exception);
        }
    }

    private static bool IsHostNotFound(Exception exception)
    {
        for (var current = exception;
             current != null;
             current = current.InnerException)
        {
            if (current is SocketException socket
                && socket.SocketErrorCode == SocketError.HostNotFound)
            {
                return true;
            }

            if (current.Message.Contains(
                    "No such host",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameUrl(string left, string right)
    {
        return string.Equals(
            left?.TrimEnd('/'),
            right?.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfPresent(FileInfo file)
    {
        file.Refresh();
        if (file.Exists)
        {
            file.Delete();
        }
    }
}

/// <summary>
/// Data passed to the command-layer bridge runner. The runner must write the
/// bridge output to <see cref="IndexFile"/> and return true only on success.
/// </summary>
public sealed class RemoteToolsRefreshRequest
{
    public RemoteToolsRefreshRequest(
        string bridgeUrl,
        string workingDirectory,
        string cliVersion,
        string commandLine,
        FileInfo indexFile)
    {
        BridgeUrl = bridgeUrl;
        WorkingDirectory = workingDirectory;
        CliVersion = cliVersion;
        CommandLine = commandLine;
        IndexFile = indexFile;
    }

    public string BridgeUrl { get; }

    public string WorkingDirectory { get; }

    public string CliVersion { get; }

    public string CommandLine { get; }

    public FileInfo IndexFile { get; }
}

public sealed class RemoteToolResolution
{
    public RemoteToolResolution(
        string url,
        string versionKey,
        string toolName,
        string label,
        bool hasSpecificError,
        bool hasExplicitVersionError)
    {
        Url = url;
        VersionKey = versionKey;
        ToolName = toolName;
        Label = label;
        HasSpecificError = hasSpecificError;
        HasExplicitVersionError = hasExplicitVersionError;
    }

    public string Url { get; }

    public string VersionKey { get; }

    public string ToolName { get; }

    public string Label { get; }

    public bool HasSpecificError { get; }

    public bool HasExplicitVersionError { get; }

    public void Deconstruct(
        out string url,
        out string versionKey,
        out string toolName,
        out string label)
    {
        url = Url;
        versionKey = VersionKey;
        toolName = ToolName;
        label = Label;
    }

    internal static RemoteToolResolution SpecificError(
        string toolName,
        bool hasExplicitVersionError)
    {
        return new RemoteToolResolution(
            null,
            null,
            toolName,
            null,
            hasSpecificError: true,
            hasExplicitVersionError: hasExplicitVersionError);
    }
}

public sealed class RemoteToolVersionList
{
    public RemoteToolVersionList(
        string toolName,
        IReadOnlyList<RemoteToolVersion> versions)
    {
        ToolName = toolName;
        Versions = versions;
    }

    public string ToolName { get; }

    public IReadOnlyList<RemoteToolVersion> Versions { get; }
}

public sealed class RemoteToolVersion
{
    public RemoteToolVersion(
        string versionKey,
        string url,
        bool isHead)
    {
        VersionKey = versionKey;
        Url = url;
        IsHead = isHead;
    }

    public string VersionKey { get; }

    public string Url { get; }

    public bool IsHead { get; }
}

public sealed record RemoteCatalogTool(
    string CanonicalName,
    string ShortName,
    string HeadVersion);
