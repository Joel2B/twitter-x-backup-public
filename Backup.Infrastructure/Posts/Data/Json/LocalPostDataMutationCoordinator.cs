using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Data.Json;

internal sealed class LocalPostDataMutationCoordinator(
    IPostStoreMergeMutationService postStoreMergeMutationService,
    IPostSoftDeleteExecutionService postSoftDeleteExecutionService,
    IPostSnapshotNormalizationService postSnapshotNormalizationService,
    IPostChangeComputationService postChangeComputationService,
    IPostChangeReadModelProjectionService postChangeReadModelProjectionService
)
{
    private readonly IPostStoreMergeMutationService _postStoreMergeMutationService =
        postStoreMergeMutationService;
    private readonly IPostSoftDeleteExecutionService _postSoftDeleteExecutionService =
        postSoftDeleteExecutionService;
    private readonly IPostSnapshotNormalizationService _postSnapshotNormalizationService =
        postSnapshotNormalizationService;
    private readonly IPostChangeComputationService _postChangeComputationService =
        postChangeComputationService;
    private readonly IPostChangeReadModelProjectionService _postChangeReadModelProjectionService =
        postChangeReadModelProjectionService;

    public IReadOnlyList<PostStoreMergeMutation> BuildMergeMutations(
        string userId,
        string origin,
        IReadOnlyList<Backup.Domain.Posts.Post> incomingDomain,
        IReadOnlyDictionary<string, Backup.Domain.Posts.Post> existingDomain,
        Backup.Domain.Posts.MergeOptions options
    ) =>
        _postStoreMergeMutationService.BuildMergeMutations(
            userId,
            origin,
            incomingDomain,
            existingDomain,
            options
        );

    public IReadOnlySet<string> SelectIdsToMarkDeleted(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds,
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts,
        IReadOnlyDictionary<string, bool> deletedById
    ) =>
        _postSoftDeleteExecutionService.SelectIdsToMarkDeleted(
            userId,
            origin,
            keepPostIds,
            domainPosts,
            deletedById
        );

    public IReadOnlyList<Post> Normalize(IReadOnlyCollection<Post> posts) =>
        PostSnapshotNormalizationAdapter.Normalize(_postSnapshotNormalizationService, posts);

    public IReadOnlyList<PostComputedChange> ComputeChanges(Backup.Domain.Posts.Post domainPost) =>
        _postChangeComputationService.Compute(domainPost);

    public IReadOnlyList<Backup.Domain.Posts.Change> ProjectChanges(
        Backup.Domain.Posts.Post domainCurrent,
        IReadOnlyList<PostChangeReplayEntry> replayEntries
    ) => _postChangeReadModelProjectionService.Project(domainCurrent, replayEntries);
}
