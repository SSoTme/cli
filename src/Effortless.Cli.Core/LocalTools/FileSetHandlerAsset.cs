#nullable enable
using System.Reflection;

namespace Effortless.Cli.LocalTools;

/// <summary>
/// <c>lib/fileset-handler.mjs</c> is embedded in the CLI and written next to
/// the host's work files so a node tool can import it through
/// <c>EFFORTLESS_FILESET_HANDLER</c> without an npm install.
/// </summary>
internal static class FileSetHandlerAsset
{
    public const string ResourceName = "Effortless.Cli.Core.lib.fileset-handler.mjs";
    public const string FileName = "fileset-handler.mjs";

    public static string Extract(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        using var stream = typeof(FileSetHandlerAsset).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing from the CLI build.");
        using var reader = new StreamReader(stream);
        var contents = reader.ReadToEnd();
        if (!File.Exists(path) || File.ReadAllText(path) != contents)
        {
            File.WriteAllText(path, contents);
        }

        return path;
    }
}
