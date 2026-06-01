using System.Reflection;
using Backup.Infrastructure.Models.Config;
using Microsoft.Extensions.Configuration;

namespace Backup.Tests;

internal static class ConfigApiTestSupport
{
    internal static AppConfig LoadSplit(string configDirectory)
    {
        MethodInfo? method = typeof(ConfigLoader).GetMethod(
            "LoadSplit",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(method);

        object? result = method!.Invoke(null, [configDirectory]);

        Assert.NotNull(result);
        Assert.IsType<AppConfig>(result);

        return (AppConfig)result!;
    }

    internal static T LoadFile<T>(string path)
    {
        IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(path).Build();

        return config.Get<T>() ?? throw new Exception($"Unable to load '{path}'");
    }

    internal static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Backup.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new Exception("Repository root not found.");
    }
}
