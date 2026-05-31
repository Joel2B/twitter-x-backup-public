using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public sealed class ConfigApiProjectionService : IConfigApiProjectionService
{
    public IReadOnlyList<ConfigApiEntry> ToEntries(
        IReadOnlyDictionary<string, ConfigApiProjection> projections
    ) =>
        projections
            .Select(kvp =>
            {
                ConfigApiProjection source = kvp.Value;
                return new ConfigApiEntry
                {
                    Key = source.Key,
                    Id = source.Id,
                    Url = source.Url,
                    Variables = source.Variables,
                    Features = source.Features,
                    FieldToggles = source.FieldToggles,
                    Headers = source.Headers,
                };
            })
            .ToList();

    public IReadOnlyDictionary<string, ConfigApiProjection> ToProjections(
        IReadOnlyList<ConfigApiEntry> entries
    ) =>
        entries.ToDictionary(
            entry => entry.Key,
            entry =>
                new ConfigApiProjection
                {
                    Key = entry.Key,
                    Id = entry.Id,
                    Url = entry.Url,
                    Variables = entry.Variables,
                    Features = entry.Features,
                    FieldToggles = entry.FieldToggles,
                    Headers = entry.Headers,
                }
        );
}
