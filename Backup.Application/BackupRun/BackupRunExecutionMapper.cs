using Backup.Application.BackupRun.Models;
using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun;

public sealed class BackupRunExecutionMapper : IBackupRunExecutionMapper
{
    public BackupRunSourceExecution MapSource(string userId, BackupRunSourcePlan source) =>
        new()
        {
            UserId = userId,
            SourceId = source.SourceId,
            ApiId = source.ApiId,
            Count = source.Count,
            Request = MapRequest(source.Request),
        };

    public BackupRunRecoveryExecution MapRecovery(BackupRunUserPlan user) =>
        new()
        {
            UserId = user.UserId,
            Api = user.Api.ToDictionary(kvp => kvp.Key, kvp => MapApi(kvp.Value)),
        };

    private static BackupRunApiExecution MapApi(BackupRunApiPlan api) =>
        new()
        {
            Id = api.Id,
            Enabled = api.Enabled,
            Request = MapRequest(api.Request),
        };

    private static BackupRunRequestExecution MapRequest(BackupRunRequestPlan request) =>
        new()
        {
            Url = request.Url,
            Variables = request.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Features = request.Features.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            FieldToggles = request.FieldToggles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
