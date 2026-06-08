using Backup.Api.Models;
using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Api.Services;

public sealed class ConfigOperationsService(ConfigContextResolver contextResolver)
{
    private readonly ConfigContextResolver _contextResolver = contextResolver;

    public ConfigSummary GetSummary() => MapSummary(_contextResolver.GetSnapshot());

    public ConfigSummary RefreshSummary() => MapSummary(_contextResolver.RefreshSnapshot());

    public IReadOnlyList<ConfigUserSummary> GetUsers() =>
        MapUsers(_contextResolver.GetSnapshot().Value.UsersContext);

    public IReadOnlyDictionary<string, int> GetFetchCounts() =>
        new Dictionary<string, int>(
            _contextResolver
                .GetSnapshot()
                .Value.Fetch.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Count,
                    StringComparer.Ordinal
                ),
            StringComparer.Ordinal
        );

    public ConfigStoresSummary GetStores()
    {
        AppConfig config = _contextResolver.GetSnapshot().Value;

        return new ConfigStoresSummary
        {
            PostStores = MapStores(config.Data.Post),
            DumpStores = MapStores(config.Data.Dump),
            BulkStores = MapStores(config.Data.Bulk),
            MediaStores = MapStores(config.Data.Media),
            BackupStores = MapStores(config.Data.Backup),
            Partitions = config.Data.Partitions.Select(MapPartition).ToList(),
        };
    }

    private static ConfigSummary MapSummary(AppConfigSnapshot snapshot)
    {
        AppConfig config = snapshot.Value;

        return new ConfigSummary
        {
            Version = snapshot.Version,
            LoadedAt = snapshot.LoadedAt,
            Users = MapUsers(config.UsersContext),
            PostStores = MapStores(config.Data.Post),
            DumpStores = MapStores(config.Data.Dump),
            BulkStores = MapStores(config.Data.Bulk),
            MediaStores = MapStores(config.Data.Media),
            BackupStores = MapStores(config.Data.Backup),
            Partitions = config.Data.Partitions.Select(MapPartition).ToList(),
            FetchCounts = new Dictionary<string, int>(
                config.Fetch.ToDictionary(entry => entry.Key, entry => entry.Value.Count),
                StringComparer.Ordinal
            ),
            BulkEnabled = config.Bulk.Enabled,
            MediaEnabled = config.Medias.Enabled,
        };
    }

    private static IReadOnlyList<ConfigUserSummary> MapUsers(
        IEnumerable<Backup.Infrastructure.Models.Config.Api.UsersContext> users
    ) =>
        users
            .Select(user => new ConfigUserSummary
            {
                UserId = user.UserId,
                Sources = user
                    .Api.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(source => new ConfigUserSourceSummary
                    {
                        SourceId = source.Key,
                        ApiId = source.Value.Id,
                        Enabled = source.Value.Enabled,
                    })
                    .ToList(),
            })
            .ToList();

    private static IReadOnlyList<StoreSummary> MapStores<TStorage>(IEnumerable<TStorage> stores)
        where TStorage : Storage =>
        stores
            .Select(store => new StoreSummary
            {
                Id = store.Id,
                Type = store.Type,
                Enabled = store.Enabled,
                IsDefault = store.Default,
                Partitions = [.. store.Partitions],
            })
            .ToList();

    private static PartitionSummary MapPartition(PartitionConfig partition) =>
        new()
        {
            Id = partition.Id,
            Name = partition.Name,
            Type = partition.Type,
            Enabled = partition.Enabled,
            Size = partition.Size,
            UsableSpace = partition.UsableSpace,
            Paths = [.. partition.Paths],
            Tags = partition.Tags,
        };
}
