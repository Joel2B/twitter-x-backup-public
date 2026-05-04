namespace Backup.App.Models.Config.Request;

public static class RequestMerge
{
    public static Request? Build(IReadOnlyDictionary<string, Api.Api> requests, string key)
    {
        if (!requests.TryGetValue(key, out Api.Api? source) || !source.Enabled)
            return null;

        Request request = source.Request.Clone();
        Query query = RequestMergeUtils.EnsureQuery(request);
        RequestMergeUtils.NormalizeVariables(query.Variables);

        return request;
    }
}
