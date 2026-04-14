namespace Backup.App.Models.Config.Request;

public static class RequestMerge
{
    public static Request Build(Request current, Request source)
    {
        Request merged = current.Clone();
        MergeInto(merged, source);
        return merged;
    }

    public static void MergeInto(Request current, Request source)
    {
        current.Query ??= new Query
        {
            Variables = [],
            Features = [],
            FieldToggles = [],
        };
        source.Query ??= new Query
        {
            Variables = [],
            Features = [],
            FieldToggles = [],
        };
        current.Headers ??= [];
        source.Headers ??= [];

        if (!string.IsNullOrWhiteSpace(source.Url))
            current.Url = source.Url;

        MergeQuery(current.Query, source.Query);
        MergeStringMap(current.Headers, source.Headers);
    }

    private static void MergeQuery(Query current, Query source)
    {
        current.Variables ??= [];
        source.Variables ??= [];
        current.Features ??= [];
        source.Features ??= [];
        current.FieldToggles ??= [];
        source.FieldToggles ??= [];

        NormalizeVariables(current.Variables);

        foreach (var kvp in source.Variables)
            current.Variables[kvp.Key] = Coerce(kvp.Value);

        foreach (var kvp in source.Features)
            current.Features[kvp.Key] = kvp.Value;

        foreach (var kvp in source.FieldToggles)
            current.FieldToggles[kvp.Key] = kvp.Value;
    }

    private static void MergeStringMap(
        Dictionary<string, string> current,
        Dictionary<string, string> source
    )
    {
        foreach (var kvp in source)
            current[kvp.Key] = kvp.Value;
    }

    private static void NormalizeVariables(Dictionary<string, object?> variables)
    {
        List<string> keys = [.. variables.Keys];

        foreach (string key in keys)
            variables[key] = Coerce(variables[key]);
    }

    private static object? Coerce(object? value)
    {
        if (value is not string text)
            return value;

        if (bool.TryParse(text, out bool boolValue))
            return boolValue;

        if (int.TryParse(text, out int intValue))
            return intValue;

        if (long.TryParse(text, out long longValue))
            return longValue;

        if (double.TryParse(text, out double doubleValue))
            return doubleValue;

        return text;
    }
}
