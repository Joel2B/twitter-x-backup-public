using Backup.Application.Config;

namespace Backup.Infrastructure.Models.Config.Request;

internal static class RequestMergeUtils
{
    internal static Query EnsureQuery(Request request)
    {
        request.Query ??= new Query
        {
            Variables = [],
            Features = [],
            FieldToggles = [],
        };

        request.Query.Variables ??= [];
        request.Query.Features ??= [];
        request.Query.FieldToggles ??= [];

        return request.Query;
    }

    internal static void MergeStringMap(
        Dictionary<string, string> current,
        IReadOnlyDictionary<string, string> source
    )
    {
        foreach (var kvp in source)
            current[kvp.Key] = kvp.Value;
    }

    internal static void NormalizeVariables(Dictionary<string, object?> variables)
    {
        QueryVariableNormalizer.Normalize(variables);
    }
}
