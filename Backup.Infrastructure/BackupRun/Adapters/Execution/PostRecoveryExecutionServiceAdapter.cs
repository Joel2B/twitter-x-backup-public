using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Abstractions.Services;

namespace Backup.Infrastructure.BackupRun.Adapters;

public sealed class PostRecoveryExecutionServiceAdapter(
    IPostService postService,
    IBackupRunExecutionContextMapper contextMapper
) : IPostRecoveryExecutionService
{
    private readonly IPostService _postService = postService;
    private readonly IBackupRunExecutionContextMapper _contextMapper = contextMapper;

    public Task Recover(
        BackupRunRecoveryExecution execution,
        CancellationToken cancellationToken = default
    ) => _postService.Recover(_contextMapper.ToUsersContext(execution), cancellationToken);
}
