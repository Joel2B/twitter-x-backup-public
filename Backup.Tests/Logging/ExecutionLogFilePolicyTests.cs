using Backup.Infrastructure.DependencyInjection.Base;

namespace Backup.Tests;

public class ExecutionLogFilePolicyTests
{
    [Fact]
    public void BuildFileName_UsesServiceAndExecutionTimestamp()
    {
        DateTimeOffset startedAt = new(2026, 11, 7, 0, 31, 57, 428, TimeSpan.FromHours(-7));

        string fileName = ExecutionLogFilePolicy.BuildFileName("cli", startedAt);

        Assert.Equal("cli-20261107-003157-428.log", fileName);
    }

    [Fact]
    public void GetPathsToRemove_ReservesSlotAndOnlyPrunesMatchingService()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string[] cliFiles = Enumerable
                .Range(1, 30)
                .Select(index => Path.Combine(directory, $"cli-20261107-000000-{index:000}.log"))
                .ToArray();

            foreach (string path in cliFiles)
                File.WriteAllText(path, "cli");

            File.WriteAllText(Path.Combine(directory, "api-20261107-000000-001.log"), "api");
            File.WriteAllText(Path.Combine(directory, "app-20261107.log"), "legacy");

            IReadOnlyList<string> paths = ExecutionLogFilePolicy.GetPathsToRemove(
                directory,
                "cli",
                retainedCount: 30
            );

            Assert.Equal([cliFiles[0]], paths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void GetPathsToRemove_AppliesApiRetentionIndependently()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            string[] apiFiles = Enumerable
                .Range(1, 10)
                .Select(index => Path.Combine(directory, $"api-20261107-000000-{index:000}.log"))
                .ToArray();

            foreach (string path in apiFiles)
                File.WriteAllText(path, "api");

            IReadOnlyList<string> paths = ExecutionLogFilePolicy.GetPathsToRemove(
                directory,
                "api",
                retainedCount: 10
            );

            Assert.Equal([apiFiles[0]], paths);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
