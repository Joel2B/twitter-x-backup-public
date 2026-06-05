using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public sealed class ConfigApiCompositionService(ConfigNormalizationService normalization)
    : IConfigApiCompositionService
{
    private readonly ConfigNormalizationService _normalization = normalization;

    public IReadOnlyDictionary<string, ConfigApiProjection> NormalizeApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections
    )
    {
        List<ConfigApiEntry> entries = [.. ToEntries(projections)];
        _normalization.ValidateAndNormalizeApi(entries);
        return ToProjections(entries);
    }

    public IReadOnlyDictionary<string, ConfigApiProjection> ApplyFetchToApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections,
        IReadOnlyList<ConfigFetchEntry> fetchEntries
    )
    {
        List<ConfigApiEntry> apiEntries = [.. ToEntries(projections)];
        _normalization.ApplyFetchToApi(apiEntries, fetchEntries);
        return ToProjections(apiEntries);
    }

    private static IReadOnlyList<ConfigApiEntry> ToEntries(
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

    private static IReadOnlyDictionary<string, ConfigApiProjection> ToProjections(
        IReadOnlyList<ConfigApiEntry> entries
    ) =>
        entries.ToDictionary(
            entry => entry.Key,
            entry => new ConfigApiProjection
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
