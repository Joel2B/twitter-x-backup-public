using Backup.Application.IO;
using Backup.Application.Media.Maintenance;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data.Media;

namespace Backup.Infrastructure.Media.Data;

public sealed class LocalMediaCacheDependencies(
    StorageMedia config,
    IPartition partition,
    IDataStoreGuardService dataStoreGuardService,
    IMediaCacheDirectoryPolicyService mediaCacheDirectoryPolicyService,
    IMediaCacheLoadExecutionService mediaCacheLoadExecutionService,
    IMediaCacheRecheckProbeExecutionService mediaCacheRecheckProbeExecutionService,
    IMediaCacheRecheckMutationExecutionService mediaCacheRecheckMutationExecutionService,
    IMediaCachePersistenceIOService mediaCachePersistenceIOService,
    IMediaCacheEntryPathPolicyService mediaCacheEntryPathPolicyService,
    IMediaCacheWritePolicyService mediaCacheWritePolicyService,
    IMediaCacheConflictResolutionService mediaCacheConflictResolutionService,
    IMediaCachePartitionSelectionService mediaCachePartitionSelectionService,
    IMediaCacheStoredEntryProjectionService mediaCacheStoredEntryProjectionService,
    IMediaCachePartitionSizeAggregationService mediaCachePartitionSizeAggregationService,
    IMediaCacheReplicationPathService mediaCacheReplicationPathService
)
{
    public StorageMedia Config { get; } = config;
    public IPartition Partition { get; } = partition;
    public IDataStoreGuardService DataStoreGuardService { get; } = dataStoreGuardService;
    public IMediaCacheDirectoryPolicyService MediaCacheDirectoryPolicyService { get; } =
        mediaCacheDirectoryPolicyService;
    public IMediaCacheLoadExecutionService MediaCacheLoadExecutionService { get; } =
        mediaCacheLoadExecutionService;
    public IMediaCacheRecheckProbeExecutionService MediaCacheRecheckProbeExecutionService { get; } =
        mediaCacheRecheckProbeExecutionService;
    public IMediaCacheRecheckMutationExecutionService MediaCacheRecheckMutationExecutionService { get; } =
        mediaCacheRecheckMutationExecutionService;
    public IMediaCachePersistenceIOService MediaCachePersistenceIOService { get; } =
        mediaCachePersistenceIOService;
    public IMediaCacheEntryPathPolicyService MediaCacheEntryPathPolicyService { get; } =
        mediaCacheEntryPathPolicyService;
    public IMediaCacheWritePolicyService MediaCacheWritePolicyService { get; } =
        mediaCacheWritePolicyService;
    public IMediaCacheConflictResolutionService MediaCacheConflictResolutionService { get; } =
        mediaCacheConflictResolutionService;
    public IMediaCachePartitionSelectionService MediaCachePartitionSelectionService { get; } =
        mediaCachePartitionSelectionService;
    public IMediaCacheStoredEntryProjectionService MediaCacheStoredEntryProjectionService { get; } =
        mediaCacheStoredEntryProjectionService;
    public IMediaCachePartitionSizeAggregationService MediaCachePartitionSizeAggregationService { get; } =
        mediaCachePartitionSizeAggregationService;
    public IMediaCacheReplicationPathService MediaCacheReplicationPathService { get; } =
        mediaCacheReplicationPathService;
}
