using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public sealed class ConfigApiCompositionService(
    ConfigNormalizationService normalization,
    IConfigApiProjectionService projectionService
) : IConfigApiCompositionService
{
    private readonly ConfigNormalizationService _normalization = normalization;
    private readonly IConfigApiProjectionService _projectionService = projectionService;

    public IReadOnlyDictionary<string, ConfigApiProjection> NormalizeApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections
    )
    {
        List<ConfigApiEntry> entries = [.. _projectionService.ToEntries(projections)];
        _normalization.ValidateAndNormalizeApi(entries);
        return _projectionService.ToProjections(entries);
    }

    public IReadOnlyDictionary<string, ConfigApiProjection> ApplyFetchToApi(
        IReadOnlyDictionary<string, ConfigApiProjection> projections,
        IReadOnlyList<ConfigFetchEntry> fetchEntries
    )
    {
        List<ConfigApiEntry> apiEntries = [.. _projectionService.ToEntries(projections)];
        _normalization.ApplyFetchToApi(apiEntries, fetchEntries);
        return _projectionService.ToProjections(apiEntries);
    }
}
