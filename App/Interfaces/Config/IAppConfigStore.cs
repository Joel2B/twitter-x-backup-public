namespace Backup.App.Interfaces.Config;

public interface IAppConfigStore
{
    public Models.Config.App Load();
    public Models.Config.Data.Data LoadData();
    public void SaveData(Models.Config.Data.Data data);
}
