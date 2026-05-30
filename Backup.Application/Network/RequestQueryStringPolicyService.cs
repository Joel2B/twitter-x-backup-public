using Newtonsoft.Json;

namespace Backup.Application.Network;

public sealed class RequestQueryStringPolicyService : IRequestQueryStringPolicyService
{
    public string Build(
        string baseUrl,
        IReadOnlyDictionary<string, object?> variables,
        IReadOnlyDictionary<string, bool> features,
        IReadOnlyDictionary<string, bool> fieldToggles
    )
    {
        Dictionary<string, object?> filteredVariables = variables
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Dictionary<string, string> queryBuilder = new()
        {
            ["variables"] = JsonConvert.SerializeObject(filteredVariables),
            ["features"] = JsonConvert.SerializeObject(features),
            ["fieldToggles"] = JsonConvert.SerializeObject(fieldToggles),
        };

        string queryUri = queryBuilder.Aggregate(
            "?",
            (query, current) => $"{query}{current.Key}={Uri.EscapeDataString(current.Value)}&"
        )[..^1];

        return $"{baseUrl}{queryUri}";
    }
}
