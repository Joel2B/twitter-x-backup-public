namespace Backup.Tests;

public sealed class PublicExamplesSanitizationTests
{
    [Fact]
    public void PublicExamples_DoNotContainKnownProductionValues()
    {
        string root = FindRepoRoot(AppContext.BaseDirectory);
        string[] files =
        [
            Path.Combine(root, "Config.example", "Services.json"),
            Path.Combine(root, "Backup.Api", "Api", "Swagger", "PostsApiExamplesOperationFilter.cs"),
            Path.Combine(root, "Backup.Api", "Api", "Swagger", "OperationsApiExamplesOperationFilter.cs"),
        ];
        string content = string.Join('\n', files.Select(File.ReadAllText));

        foreach (string value in new[]
        {
            "1539842716054821190",
            "44196397",
            "elonmusk",
            "Elon Musk",
            "nas L:",
            "nas I:",
            "/mnt/l/apps/twitter-x-backup",
        })
            Assert.DoesNotContain(value, content, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot(string startPath)
    {
        DirectoryInfo? current = new(startPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Backup.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root (Backup.sln).");
    }
}
