using Backup.Infrastructure.Interfaces.Config;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Services.Config;

public sealed class JsonAppConfigStore : IAppConfigStore
{
    public AppConfig Load() => ConfigLoader.Load();

    public DataConfig LoadData() => ConfigLoader.LoadData();

    public void SaveData(DataConfig data) => ConfigLoader.SaveData(data);
}

