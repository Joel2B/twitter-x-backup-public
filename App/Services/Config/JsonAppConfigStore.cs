using Backup.App.Interfaces.Config;
using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;

namespace Backup.App.Services.Config;

public sealed class JsonAppConfigStore : IAppConfigStore
{
    public AppConfig Load() => ConfigLoader.Load();

    public DataConfig LoadData() => ConfigLoader.LoadData();

    public void SaveData(DataConfig data) => ConfigLoader.SaveData(data);
}
