using System.Globalization;

namespace Backup.App.Models.Config.Request;

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
        List<string> keys = [.. variables.Keys];

        foreach (string key in keys)
            variables[key] = Coerce(variables[key]);
    }

    internal static object? Coerce(object? value)
    {
        if (value is not string text)
            return value;

        if (bool.TryParse(text, out bool boolValue))
            return boolValue;

        if (
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue)
        )
            return intValue;

        if (
            long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long longValue
            )
        )
            return longValue;

        if (
            double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out double doubleValue
            )
        )
            return doubleValue;

        return text;
    }
}
