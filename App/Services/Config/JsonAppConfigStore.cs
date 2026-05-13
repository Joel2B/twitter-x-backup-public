using Backup.App.Interfaces.Config;
using Backup.App.Models.Config;

namespace Backup.App.Services.Config;

public sealed class JsonAppConfigStore : IAppConfigStore
{
    public Models.Config.App Load() => ConfigLoader.Load();

    public Models.Config.Data.Data LoadData() => ConfigLoader.LoadData();

    public void SaveData(Models.Config.Data.Data data) => ConfigLoader.SaveData(data);
}
