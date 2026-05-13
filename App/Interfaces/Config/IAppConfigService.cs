namespace Backup.App.Interfaces.Config;

public interface IAppConfigService
{
    public AppConfigSnapshot GetSnapshot();
    public AppConfigSnapshot Refresh();
    public void SaveData(Models.Config.Data.Data data, bool refreshSnapshot = false);
}
