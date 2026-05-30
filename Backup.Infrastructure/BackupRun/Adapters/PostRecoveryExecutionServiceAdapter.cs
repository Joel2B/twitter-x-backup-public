using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Abstractions.Services;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class PostRecoveryExecutionServiceAdapter(IPostService postService)
    : IPostRecoveryExecutionService
{
    private readonly IPostService _postService = postService;

    public Task Recover(BackupRunRecoveryExecution execution) =>
        _postService.Recover(
            new UsersContext
            {
                UserId = execution.UserId,
                Api = execution.Api.ToDictionary(kvp => kvp.Key, kvp => ToApiConfig(kvp.Value)),
            }
        );

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
