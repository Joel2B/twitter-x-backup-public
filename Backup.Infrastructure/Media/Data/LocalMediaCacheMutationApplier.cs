using System.Collections.Concurrent;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Media.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Data;

internal sealed class LocalMediaCacheMutationApplier(
    ILogger logger,
    IMediaCacheRecheckMutationExecutionService mediaCacheRecheckMutationExecutionService
)
{
    private readonly ILogger _logger = logger;
    private readonly IMediaCacheRecheckMutationExecutionService _mediaCacheRecheckMutationExecutionService =
        mediaCacheRecheckMutationExecutionService;

    public void Apply(
        ConcurrentDictionary<string, MediaCacheEntry> cache,
        IReadOnlyList<MediaCacheRecheckMutation> mutations
    )
    {
        MediaCacheRecheckMutationApplySelection selection =
            _mediaCacheRecheckMutationExecutionService.Execute(
                mutations,
                cache.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            );

        foreach (string path in selection.InvalidPaths)
            _logger.LogError("invalid recheck evaluation for path {path}", path);

        foreach (string path in selection.RemoveExistingPaths)
        {
            cache.TryRemove(path, out _);
            _logger.LogWarning("{path} path removed from cache", path);
        }

        foreach (string path in selection.RemoveMissingPaths)
            _logger.LogError("error removing path {path}", path);

        foreach (MediaCacheEntryState state in selection.UpdateExistingEntries)
        {
            cache.TryGetValue(state.Path, out MediaCacheEntry? old);
            MediaCacheEntry updated = LocalMediaCacheEntryMapper.ToCacheEntry(state);
            if (old is not null)
                cache.TryUpdate(state.Path, updated, old);
        }

        foreach (string path in selection.UpdateMissingPaths)
            _logger.LogError("error updating path {path}", path);
    }
}
