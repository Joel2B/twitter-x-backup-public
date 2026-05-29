using Backup.App.Models.Config.Data;

namespace Backup.App.Interfaces.Config;

public interface IAppConfigService
{
    public AppConfigSnapshot GetSnapshot();
    public AppConfigSnapshot Refresh();
    public void SaveData(DataConfig data, bool refreshSnapshot = false);
}
