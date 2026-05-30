using Backup.Domain.BackupRun;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;

namespace Backup.Infrastructure.BackupRun.Adapters;

internal static class BackupRunPlanMapper
{
    public static BackupRunRequestPlan ToPlanRequest(Request request) =>
        new()
        {
            Url = request.Url,
            Variables = request.Query.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Features = request.Query.Features.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            FieldToggles = request.Query.FieldToggles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };

    public static Request ToRequest(BackupRunRequestPlan request) =>
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

    public static ApiContext ToApiContext(string userId, BackupRunSourcePlan source) =>
        new()
        {
            Id = source.ApiId,
            Request = ToRequest(source.Request),
            Count = source.Count,
            UserId = userId,
        };

    public static UsersContext ToUsersContext(BackupRunUserPlan user) =>
        new()
        {
            UserId = user.UserId,
            Api = user.Api.ToDictionary(kvp => kvp.Key, kvp => ToApiConfig(kvp.Value)),
        };

    private static ApiConfig ToApiConfig(BackupRunApiPlan api) =>
        new() { Id = api.Id, Enabled = api.Enabled, Request = ToRequest(api.Request) };
}
