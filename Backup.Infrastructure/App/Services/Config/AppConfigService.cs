using Backup.App.Interfaces.Config;
using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;

namespace Backup.App.Services.Config;

public sealed class AppConfigService(IAppConfigStore store) : IAppConfigService
{
    private readonly IAppConfigStore _store = store;
    private readonly Lock _sync = new();
    private long _version;
    private AppConfigSnapshot _snapshot = BuildSnapshot(0, store.Load());

    public AppConfigSnapshot GetSnapshot()
    {
        lock (_sync)
            return _snapshot;
    }

    public AppConfigSnapshot Refresh()
    {
        lock (_sync)
            return RefreshCore();
    }

    public void SaveData(DataConfig data, bool refreshSnapshot = false)
    {
        lock (_sync)
        {
            _store.SaveData(data);

            if (refreshSnapshot)
                RefreshCore();
        }
    }

    private AppConfigSnapshot RefreshCore()
    {
        long version = _version + 1;
        AppConfigSnapshot snapshot = BuildSnapshot(version, _store.Load());

        _version = version;
        _snapshot = snapshot;

        return snapshot;
    }

    private static AppConfigSnapshot BuildSnapshot(long version, AppConfig config) =>
        new(version, DateTimeOffset.UtcNow, config);
}
