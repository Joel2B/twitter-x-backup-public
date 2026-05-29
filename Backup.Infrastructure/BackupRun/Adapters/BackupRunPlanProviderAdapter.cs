using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class BackupRunPlanProviderAdapter(AppConfig config) : IBackupRunPlanProvider
{
    private readonly AppConfig _config = config;

    public BackupRunPlan GetPlan()
    {
        List<BackupRunUserPlan> users = _config
            .UsersContext.Select(
                (context, index) =>
                    new BackupRunUserPlan
                    {
                        UserId = context.UserId,
                        Api = context.Api.ToDictionary(
                            kvp => kvp.Key,
                            kvp => new BackupRunApiPlan
                            {
                                Id = kvp.Value.Id,
                                Enabled = kvp.Value.Enabled,
                                Request = BackupRunPlanMapper.ToPlanRequest(kvp.Value.Request),
                            }
                        ),
                        Sources = GetSources(context),
                        RunRecovery = index == 0,
                        RunBulk = index == 0,
                    }
            )
            .ToList();

        return new BackupRunPlan
        {
            Users = users,
            IsBulkEnabled = _config.Bulk.Enabled,
            IsMediaEnabled = _config.Medias.Enabled,
        };
    }

    private IReadOnlyList<BackupRunSourcePlan> GetSources(UsersContext context)
    {
        List<BackupRunSourcePlan> sources = [];

        foreach ((string sourceKey, FetchItem fetchItem) in _config.Fetch)
        {
            if (!context.Api.TryGetValue(sourceKey, out ApiConfig? api))
                continue;

            if (!api.Enabled)
                continue;

            sources.Add(
                new BackupRunSourcePlan
                {
                    SourceId = sourceKey,
                    ApiId = api.Id,
                    Count = fetchItem.Count,
                    Request = BackupRunPlanMapper.ToPlanRequest(api.Request),
                }
            );
        }

        return sources;
    }
}
