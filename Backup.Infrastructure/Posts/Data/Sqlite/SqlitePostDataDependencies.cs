using Backup.Application.Core;
using Backup.Application.Posts;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public sealed class SqlitePostDataDependencies(
    IPostStoreMergeMutationService postStoreMergeMutationService,
    IPostSoftDeleteExecutionService postSoftDeleteExecutionService,
    IPostSnapshotNormalizationService postSnapshotNormalizationService,
    IPostMediaInputsCompositionService postMediaInputsCompositionService,
    IPostHashingService postHashingService,
    IPostChangeComputationService postChangeComputationService,
    IPostChangeReadModelProjectionService postChangeReadModelProjectionService,
    IPostIdentifierFilterService postIdentifierFilterService,
    IDateTimeProvider dateTimeProvider
)
{
    public IPostStoreMergeMutationService PostStoreMergeMutationService { get; } =
        postStoreMergeMutationService;
    public IPostSoftDeleteExecutionService PostSoftDeleteExecutionService { get; } =
        postSoftDeleteExecutionService;
    public IPostSnapshotNormalizationService PostSnapshotNormalizationService { get; } =
        postSnapshotNormalizationService;
    public IPostMediaInputsCompositionService PostMediaInputsCompositionService { get; } =
        postMediaInputsCompositionService;
    public IPostHashingService PostHashingService { get; } = postHashingService;
    public IPostChangeComputationService PostChangeComputationService { get; } =
        postChangeComputationService;
    public IPostChangeReadModelProjectionService PostChangeReadModelProjectionService { get; } =
        postChangeReadModelProjectionService;
    public IPostIdentifierFilterService PostIdentifierFilterService { get; } = postIdentifierFilterService;
    public IDateTimeProvider DateTimeProvider { get; } = dateTimeProvider;
}
