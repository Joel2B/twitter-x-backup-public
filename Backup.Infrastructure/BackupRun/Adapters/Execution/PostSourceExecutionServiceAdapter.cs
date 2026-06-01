using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Abstractions.Services;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class PostSourceExecutionServiceAdapter(
    IPostService postService,
    BackupRunExecutionContextMapper contextMapper
) : IPostSourceExecutionService
{
    private readonly IPostService _postService = postService;
    private readonly BackupRunExecutionContextMapper _contextMapper = contextMapper;

    public Task Download(
        BackupRunSourceExecution execution,
        CancellationToken cancellationToken = default
    ) => _postService.Download(_contextMapper.ToApiContext(execution), cancellationToken);
}
