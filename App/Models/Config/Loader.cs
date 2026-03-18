using Microsoft.Extensions.Configuration;

namespace Backup.App.Models.Config;

public static class ConfigLoader
{
    public static App Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "config", "twitter.json");

        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        App app = config.Get<App>() ?? throw new Exception("error deserializing config file");

        return app;
    }
}
