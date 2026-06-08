using Backup.Infrastructure.DependencyInjection.Features.Media;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Downloads;

namespace Backup.Infrastructure.Media.Data;

internal sealed class MediaCacheTargetRuntime(
    string key,
    string label,
    bool isDefault,
    MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType type,
    PathConfig? path,
    IMediaCachePersistenceIOService persistence
)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool IsDefault { get; } = isDefault;
    public MediaDataInfrastructureServiceCollectionExtensions.MediaCacheType Type { get; } = type;
    public PathConfig? Path { get; } = path;
    public IMediaCachePersistenceIOService Persistence { get; } = persistence;
}
