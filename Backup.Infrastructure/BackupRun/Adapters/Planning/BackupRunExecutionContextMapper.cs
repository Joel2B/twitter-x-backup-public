using Backup.Application.BackupRun.Models;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class BackupRunExecutionContextMapper : IBackupRunExecutionContextMapper
{
    public ApiContext ToApiContext(BackupRunSourceExecution execution) =>
        new()
        {
            Id = execution.ApiId,
            Count = execution.Count,
            UserId = execution.UserId,
            Request = ToRequest(execution.Request),
        };

    public UsersContext ToUsersContext(BackupRunRecoveryExecution execution) =>
        new()
        {
            UserId = execution.UserId,
            Api = execution.Api.ToDictionary(kvp => kvp.Key, kvp => ToApiConfig(kvp.Value)),
        };

    private static ApiConfig ToApiConfig(BackupRunApiExecution api) =>
        new()
        {
            Id = api.Id,
            Enabled = api.Enabled,
            Request = ToRequest(api.Request),
        };

    private static Request ToRequest(BackupRunRequestExecution request) =>
        new()
        {
            Url = request.Url,
            Query = new Query
            {
                Variables = request.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Features = request.Features.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                FieldToggles = request.FieldToggles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            },
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
