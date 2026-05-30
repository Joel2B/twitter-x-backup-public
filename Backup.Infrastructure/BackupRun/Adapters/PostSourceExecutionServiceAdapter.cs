using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Posts.Abstractions.Services;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class PostSourceExecutionServiceAdapter(IPostService postService)
    : IPostSourceExecutionService
{
    private readonly IPostService _postService = postService;

    public Task Download(BackupRunSourceExecution execution) =>
        _postService.Download(
            new ApiContext
            {
                Id = execution.ApiId,
                Count = execution.Count,
                UserId = execution.UserId,
                Request = ToRequest(execution.Request),
            }
        );

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
