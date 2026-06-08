using Backup.Application.Posts;
using Backup.Infrastructure.DependencyInjection.Composition;
using Backup.Infrastructure.Hosting;
using Backup.Infrastructure.Models.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Tests;

public class InfrastructureCompositionSmokeTests
{
    [Fact]
    public async Task AddBackupApiInfrastructure_ResolvesCriticalServices()
    {
        using TestConfigScope _ = TestConfigScope.Create();

        ServiceCollection services = new();
        services.AddBackupApiInfrastructure();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IPostRuntimeService)
        );

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        Assert.NotNull(provider.GetRequiredService<AppConfig>());
    }

    [Fact]
    public async Task AddBackupCliInfrastructure_ResolvesRunner()
    {
        using TestConfigScope _ = TestConfigScope.Create();

        ServiceCollection services = new();
        services.AddBackupCliInfrastructure();

        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<BackupCliRunner>());
    }

    private sealed class TestConfigScope : IDisposable
    {
        private readonly string _configPath;
        private readonly bool _createdByTest;

        private TestConfigScope(string configPath, bool createdByTest)
        {
            _configPath = configPath;
            _createdByTest = createdByTest;
        }

        public static TestConfigScope Create()
        {
            string baseDir = AppContext.BaseDirectory;
            string configPath = Path.Combine(baseDir, "config");
            bool createdByTest = false;

            string repoRoot = FindRepoRoot(baseDir);
            string source = ResolveConfigExampleDirectory(repoRoot);
            if (!Directory.Exists(configPath))
            {
                CopyDirectory(source, configPath);
                createdByTest = true;
            }

            return new(configPath, createdByTest);
        }

        public void Dispose()
        {
            if (_createdByTest && Directory.Exists(_configPath))
            {
                Directory.Delete(_configPath, recursive: true);
            }
        }

        private static string FindRepoRoot(string startPath)
        {
            DirectoryInfo? current = new(startPath);

            while (current is not null)
            {
                string solutionPath = Path.Combine(current.FullName, "Backup.sln");
                if (File.Exists(solutionPath))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repo root (Backup.sln).");
        }

        private static string ResolveConfigExampleDirectory(string repoRoot)
        {
            string primary = Path.Combine(repoRoot, "config.example");
            if (Directory.Exists(primary))
                return primary;

            throw new DirectoryNotFoundException(
                "Could not locate config example directory. Expected 'config.example'."
            );
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            foreach (string dir in Directory.GetDirectories(source))
            {
                string target = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectory(dir, target);
            }
        }
    }
}
