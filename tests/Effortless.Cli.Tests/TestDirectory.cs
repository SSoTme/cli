namespace Effortless.Cli.Tests;

internal sealed class TestDirectory : IDisposable
{
    public TestDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "effortless-cli-core-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relativePath)
    {
        return System.IO.Path.Combine(Path, relativePath);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Path))
        {
            return;
        }

        Directory.Delete(Path, recursive: true);
    }
}
