using Backup.Application.Config.Models;

namespace Backup.Application.Config;

public sealed class ApiRequestBuildService : IApiRequestBuildService
{
    public ApiRequestBuildResult? Build(
        IReadOnlyDictionary<string, ApiRequestBuildSource> requests,
        string key
    )
    {
        if (!requests.TryGetValue(key, out ApiRequestBuildSource? source) || !source.Enabled)
            return null;

        Dictionary<string, object?> variables = source.Variables.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );
        QueryVariableNormalizer.Normalize(variables);

        Dictionary<string, bool> features = (source.Features ?? []).ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );

        Dictionary<string, bool> fieldToggles = (source.FieldToggles ?? []).ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );

        Dictionary<string, string> headers = (source.Headers ?? []).ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
        );

        return new ApiRequestBuildResult
        {
            Url = source.Url,
            Variables = variables,
            Features = features,
            FieldToggles = fieldToggles,
            Headers = headers,
        };
    }
}
