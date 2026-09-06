using System.Diagnostics;
using System.Net;
using System.Text;
using Effortless.Cli.Seeds;

namespace Effortless.Cli.Tests;

public sealed class SeedFeatureTests
{
    [Fact(DisplayName = "unit-seed-catalog: discovery requires root effortless.json")]
    public async Task CatalogIncludesOnlyRepositoriesWithRootProject()
    {
        var handler = new SeedCatalogHandler();
        using var client = new HttpClient(handler);
        var catalog = new SeedCatalogClient(client);

        var seeds = await catalog.ListAsync("example");

        var seed = Assert.Single(seeds);
        Assert.Equal("seed-with-project", seed.Name);
        Assert.Equal("with-project", seed.ShortName);
        Assert.Contains(
            handler.Requests,
            uri => uri.AbsolutePath.EndsWith(
                "/seed-with-project/main/effortless.json",
                StringComparison.Ordinal));
    }

    [Fact(DisplayName = "unit-seed-replacements: values replace content and file names")]
    public async Task ReplacementsUpdateTextAndFileNames()
    {
        using var directory = new TestDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, "ssotme-seed.json"),
            """
            {
              "replacements": [
                {
                  "key": "project-name",
                  "description": "Project name"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(directory.Path, "seed-config-values.json"),
            """
            {
              "replacements": [
                {
                  "key": "project-name",
                  "value": "orders"
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(directory.Path, "$project-name$.txt"),
            "name=$PROJECT-NAME$");

        await new SeedReplacements().ApplyAsync(
            new DirectoryInfo(directory.Path),
            reverseUpdate: false);

        Assert.False(
            File.Exists(
                Path.Combine(
                    directory.Path,
                    "$project-name$.txt")));
        Assert.Equal(
            "name=orders",
            File.ReadAllText(
                Path.Combine(
                    directory.Path,
                    "orders.txt")));
    }

    [Fact(DisplayName = "unit-seed-clone: cloning preserves repository metadata")]
    public void ClonePreservesRepositoryMetadata()
    {
        using var directory = new TestDirectory();
        var destination = Path.Combine(
            directory.Path,
            "seed");
        ProcessStartInfo? captured = null;
        var manager = new SeedRepositoryManager(
            startInfo =>
            {
                captured = startInfo;
                Directory.CreateDirectory(destination);
                Directory.CreateDirectory(
                    Path.Combine(destination, ".git"));
                File.WriteAllText(
                    Path.Combine(
                        destination,
                        "effortless.json"),
                    """{"ProjectTranspilers":[]}""");
                return 0;
            });

        var result = manager.Clone(
            "https://github.com/example/seed.git",
            destination);

        Assert.Equal(destination, result);
        Assert.NotNull(captured);
        Assert.Equal(
            ["clone", "https://github.com/example/seed.git", destination],
            captured.ArgumentList);
        Assert.True(
            Directory.Exists(
                Path.Combine(destination, ".git")));
    }

    private sealed class SeedCatalogHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (request.RequestUri!.Host == "api.github.com")
            {
                return Response(
                    HttpStatusCode.OK,
                    """
                    [
                      {
                        "name": "seed-with-project",
                        "description": "yes",
                        "clone_url": "https://github.com/example/seed-with-project.git",
                        "default_branch": "main"
                      },
                      {
                        "name": "ordinary-repo",
                        "description": "no",
                        "clone_url": "https://github.com/example/ordinary-repo.git",
                        "default_branch": "main"
                      }
                    ]
                    """);
            }

            return request.RequestUri.AbsolutePath.Contains(
                "seed-with-project",
                StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, "{}")
                : Response(HttpStatusCode.NotFound, "");
        }

        private static Task<HttpResponseMessage> Response(
            HttpStatusCode status,
            string body) =>
            Task.FromResult(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent(
                        body,
                        Encoding.UTF8,
                        "application/json"),
                });
    }
}
