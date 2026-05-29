using Backup.App.Models.Config.Api;

namespace Backup.App.Models.Config.ApiRequest;

public static class RequestMerge
{
    public static Request? Build(IReadOnlyDictionary<string, ApiConfig> requests, string key)
    {
        if (!requests.TryGetValue(key, out ApiConfig? source) || !source.Enabled)
            return null;

        Request request = source.Request.Clone();
        Query query = RequestMergeUtils.EnsureQuery(request);
        RequestMergeUtils.NormalizeVariables(query.Variables);

        return request;
    }
}
