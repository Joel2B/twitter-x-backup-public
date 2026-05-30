using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Interfaces.Config;

public interface IAppConfigService
{
    public AppConfigSnapshot GetSnapshot();
    public AppConfigSnapshot Refresh();
    public void SaveData(DataConfig data, bool refreshSnapshot = false);
}
