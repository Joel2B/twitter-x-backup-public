namespace Backup.App.Models.Config.Request;

public static class RequestMerge
{
    public static Request Build(Request current, Request source)
    {
        Request merged = current.Clone();
        MergeInto(merged, source);
        return merged;
    }

    public static Request? Build(IReadOnlyDictionary<string, Api.Api> requests, string key)
    {
        if (!requests.TryGetValue(key, out Api.Api? source) || !source.Enabled)
            return null;

        Request request = source.Request.Clone();
        Query query = RequestMergeUtils.EnsureQuery(request);
        RequestMergeUtils.NormalizeVariables(query.Variables);

        return request;
    }

    public static void MergeInto(Request current, Request source)
    {
        Query currentQuery = RequestMergeUtils.EnsureQuery(current);
        Query sourceQuery = RequestMergeUtils.EnsureQuery(source);
        current.Headers ??= [];
        source.Headers ??= [];

        if (!string.IsNullOrWhiteSpace(source.Url))
            current.Url = source.Url;

        MergeQuery(
            currentQuery,
            sourceQuery.Variables,
            sourceQuery.Features,
            sourceQuery.FieldToggles
        );

        RequestMergeUtils.MergeStringMap(current.Headers, source.Headers);
    }

    public static void MergeInto(Request current, Api.ApiRequestOverride source)
    {
        Query currentQuery = RequestMergeUtils.EnsureQuery(current);
        current.Headers ??= [];

        if (!string.IsNullOrWhiteSpace(source.Url))
            current.Url = source.Url;

        MergeQuery(
            currentQuery,
            source.Query?.Variables,
            source.Query?.Features,
            source.Query?.FieldToggles
        );

        if (source.Headers is not null)
            RequestMergeUtils.MergeStringMap(current.Headers, source.Headers);
    }

    private static void MergeQuery(
        Query current,
        IReadOnlyDictionary<string, object?>? sourceVariables,
        IReadOnlyDictionary<string, bool>? sourceFeatures,
        IReadOnlyDictionary<string, bool>? sourceFieldToggles
    )
    {
        current.Variables ??= [];
        current.Features ??= [];
        current.FieldToggles ??= [];

        RequestMergeUtils.NormalizeVariables(current.Variables);

        if (sourceVariables is not null)
        {
            foreach (var kvp in sourceVariables)
                current.Variables[kvp.Key] = RequestMergeUtils.Coerce(kvp.Value);
        }

        if (sourceFeatures is not null)
        {
            foreach (var kvp in sourceFeatures)
                current.Features[kvp.Key] = kvp.Value;
        }

        if (sourceFieldToggles is not null)
        {
            foreach (var kvp in sourceFieldToggles)
                current.FieldToggles[kvp.Key] = kvp.Value;
        }
    }
}
