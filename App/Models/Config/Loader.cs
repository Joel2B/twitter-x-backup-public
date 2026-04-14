using Microsoft.Extensions.Configuration;

namespace Backup.App.Models.Config;

public static class ConfigLoader
{
    public static App Load()
    {
        string configDirectory = Path.Combine(AppContext.BaseDirectory, "config");

        if (HasSplitConfig(configDirectory))
            return LoadSplit(configDirectory);

        return LoadLegacy(configDirectory);
    }

    private static bool HasSplitConfig(string configDirectory) =>
        File.Exists(Path.Combine(configDirectory, "Fetch.json"));

    private static App LoadLegacy(string configDirectory)
    {
        string path = Path.Combine(configDirectory, "twitter.json");

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return config.Get<App>() ?? throw new Exception("error deserializing config file");
    }

    private static App LoadSplit(string configDirectory) =>
        new()
        {
            Fetch = LoadFile<Fetch>(configDirectory, "Fetch.json"),
            Api = LoadFile<Dictionary<string, Request.Request>>(configDirectory, "Api.json"),
            Services = LoadFile<Services>(configDirectory, "Services.json"),
            Data = LoadFile<Data.Data>(configDirectory, "Data.json"),
            Downloads = LoadFile<Downloads.Downloads>(configDirectory, "Downloads.json"),
            Medias = LoadFile<Medias.Medias>(configDirectory, "Medias.json"),
            Proxy = LoadFile<Proxy.Proxy>(configDirectory, "Proxy.json"),
            Debug = LoadFile<Debug>(configDirectory, "Debug.json"),
            Tasks = LoadFile<Tasks.Tasks>(configDirectory, "Tasks.json"),
            Bulk = LoadFile<Bulk>(configDirectory, "Bulk.json"),
            Network = LoadFile<Network>(configDirectory, "Network.json"),
        };

    private static T LoadFile<T>(string configDirectory, string fileName)
    {
        string path = Path.Combine(configDirectory, fileName);

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        return config.Get<T>() ?? throw new Exception($"error deserializing config file '{fileName}'");
    }
}
