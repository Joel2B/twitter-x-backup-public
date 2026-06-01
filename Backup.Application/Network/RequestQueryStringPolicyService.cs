using System.Text.Json;

namespace Backup.Application.Network;

public sealed class RequestQueryStringPolicyService : IRequestQueryStringPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new();

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
            ["variables"] = JsonSerializer.Serialize(filteredVariables, JsonOptions),
            ["features"] = JsonSerializer.Serialize(features, JsonOptions),
            ["fieldToggles"] = JsonSerializer.Serialize(fieldToggles, JsonOptions),
        };

        string queryUri = queryBuilder.Aggregate(
            "?",
            (query, current) => $"{query}{current.Key}={Uri.EscapeDataString(current.Value)}&"
        )[..^1];

        return $"{baseUrl}{queryUri}";
    }
}
