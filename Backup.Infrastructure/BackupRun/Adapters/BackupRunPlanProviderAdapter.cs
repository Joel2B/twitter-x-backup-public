using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class BackupRunPlanProviderAdapter(AppConfig config, IBackupRunPlanBuilder planBuilder)
    : IBackupRunPlanProvider
{
    private readonly AppConfig _config = config;
    private readonly IBackupRunPlanBuilder _planBuilder = planBuilder;

    public Backup.Domain.BackupRun.BackupRunPlan GetPlan()
    {
        BackupRunPlanInput input = new()
        {
            Users = _config.UsersContext.Select(MapUser).ToList(),
            Fetch = _config.Fetch.ToDictionary(
                kvp => kvp.Key,
                kvp => new BackupRunFetchInput { Count = kvp.Value.Count }
            ),
            IsBulkEnabled = _config.Bulk.Enabled,
            IsMediaEnabled = _config.Medias.Enabled,
        };

        return _planBuilder.Build(input);
    }

    private static BackupRunUserInput MapUser(UsersContext context) =>
        new()
        {
            UserId = context.UserId,
            Api = context.Api.ToDictionary(
                kvp => kvp.Key,
                kvp => new Backup.Domain.BackupRun.BackupRunApiPlan
                {
                    Id = kvp.Value.Id,
                    Enabled = kvp.Value.Enabled,
                    Request = BackupRunPlanMapper.ToPlanRequest(kvp.Value.Request),
                }
            ),
        };
}
