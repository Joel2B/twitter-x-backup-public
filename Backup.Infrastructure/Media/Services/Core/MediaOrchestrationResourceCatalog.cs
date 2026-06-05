using Backup.Application.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaOrchestrationResourceCatalog(
    ILogger logger,
    IEnumerable<IMediaStorage> mediaStorage,
    IEnumerable<IMediaDataMaintenance> mediaMaintenance
)
{
    private readonly ILogger _logger = logger;
    private readonly Dictionary<string, IMediaStorage> _storageById = mediaStorage
        .Where(item => !string.IsNullOrWhiteSpace(item.Id))
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IMediaDataMaintenance> _maintenanceById = mediaMaintenance
        .Where(item => item.Id is not null)
        .ToDictionary(item => item.Id!, item => item, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IMediaStorage> Storage => _storageById.Values;

    public IReadOnlyList<string> GetStorageIds() =>
        _storageById
            .Keys.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public bool HasMaintenance(string storageId)
    {
        bool has = _maintenanceById.Keys.Contains(storageId, StringComparer.OrdinalIgnoreCase);

        if (!has)
            _logger.LogWarning(
                "no media maintenance configured for media data {storageId}",
                storageId
            );

        return has;
    }

    public IMediaStorage? GetStorage(string storageId)
    {
        string? resolvedId = _storageById.Keys.FirstOrDefault(id =>
            string.Equals(id, storageId, StringComparison.OrdinalIgnoreCase)
        );

        if (resolvedId is null)
        {
            _logger.LogWarning("media storage not found: {storageId}", storageId);
            return null;
        }

        return _storageById[resolvedId];
    }

    public IMediaDataMaintenance? GetMaintenance(string storageId)
    {
        if (_maintenanceById.TryGetValue(storageId, out IMediaDataMaintenance? maintenance))
            return maintenance;

        _logger.LogWarning("media maintenance not found: {storageId}", storageId);
        return null;
    }

    public IMediaStorage? GetBackupSource()
    {
        string? storageId = GetStorageIds().FirstOrDefault();
        return storageId is null ? null : _storageById[storageId];
    }
}
