using Backup.Application.BackupRun.Models;
using Backup.Domain.BackupRun;

namespace Backup.Application.BackupRun;

public sealed class BackupRunPlanBuilder : IBackupRunPlanBuilder
{
    public BackupRunPlan Build(BackupRunPlanInput input)
    {
        List<BackupRunUserPlan> users = input.Users
            .Select(
                (user, index) =>
                    new BackupRunUserPlan
                    {
                        UserId = user.UserId,
                        Api = user.Api.ToDictionary(kvp => kvp.Key, kvp => CloneApi(kvp.Value)),
                        Sources = GetSources(user, input.Fetch),
                        RunRecovery = index == 0,
                        RunBulk = index == 0,
                    }
            )
            .ToList();

        return new BackupRunPlan
        {
            Users = users,
            IsBulkEnabled = input.IsBulkEnabled,
            IsMediaEnabled = input.IsMediaEnabled,
        };
    }

    private static IReadOnlyList<BackupRunSourcePlan> GetSources(
        BackupRunUserInput user,
        IReadOnlyDictionary<string, BackupRunFetchInput> fetch
    )
    {
        List<BackupRunSourcePlan> sources = [];

        foreach ((string sourceKey, BackupRunFetchInput fetchItem) in fetch)
        {
            if (!user.Api.TryGetValue(sourceKey, out BackupRunApiPlan? api))
                continue;

            if (!api.Enabled)
                continue;

            sources.Add(
                new BackupRunSourcePlan
                {
                    SourceId = sourceKey,
                    ApiId = api.Id,
                    Count = fetchItem.Count,
                    Request = CloneRequest(api.Request),
                }
            );
        }

        return sources;
    }

    private static BackupRunApiPlan CloneApi(BackupRunApiPlan api) =>
        new() { Id = api.Id, Enabled = api.Enabled, Request = CloneRequest(api.Request) };

    private static BackupRunRequestPlan CloneRequest(BackupRunRequestPlan request) =>
        new()
        {
            Url = request.Url,
            Variables = request.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Features = request.Features.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            FieldToggles = request.FieldToggles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
