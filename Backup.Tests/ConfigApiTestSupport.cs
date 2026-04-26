using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Backup.Tests;

internal static class ConfigApiTestSupport
{
    internal static App.Models.Config.App LoadSplit(string configDirectory)
    {
        MethodInfo? method = typeof(App.Models.Config.ConfigLoader).GetMethod(
            "LoadSplit",
            BindingFlags.NonPublic | BindingFlags.Static
        );

        Assert.NotNull(method);

        object? result = method!.Invoke(null, [configDirectory]);

        Assert.NotNull(result);
        Assert.IsType<App.Models.Config.App>(result);

        return (App.Models.Config.App)result!;
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
