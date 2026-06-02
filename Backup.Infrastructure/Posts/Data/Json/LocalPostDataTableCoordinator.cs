using Backup.Application.Posts;
using Backup.Application.Posts.Models;

namespace Backup.Infrastructure.Posts.Data.Json;

internal sealed class LocalPostDataTableCoordinator(
    IPostTableProjectionService postTableProjectionService,
    IPostTableMaterializationService postTableMaterializationService
)
{
    private readonly IPostTableProjectionService _postTableProjectionService =
        postTableProjectionService;
    private readonly IPostTableMaterializationService _postTableMaterializationService =
        postTableMaterializationService;

    public PostTableProjectionResult Project(IReadOnlyList<Backup.Domain.Posts.Post> domainPosts) =>
        _postTableProjectionService.Project(domainPosts);

    public IReadOnlyList<Backup.Domain.Posts.Post> Materialize(
        PostTableMaterializationInput input
    ) => _postTableMaterializationService.Materialize(input);
}
