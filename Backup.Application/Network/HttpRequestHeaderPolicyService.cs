namespace Backup.Application.Network;

public sealed class HttpRequestHeaderPolicyService : IHttpRequestHeaderPolicyService
{
    public void ApplyHeaders(
        HttpRequestMessage requestHttp,
        IReadOnlyDictionary<string, string> headers
    )
    {
        foreach ((string rawKey, string rawValue) in headers)
        {
            if (string.IsNullOrWhiteSpace(rawKey) || string.IsNullOrWhiteSpace(rawValue))
                continue;

            string key = rawKey.Trim();
            string value = rawValue.Trim();

            if (key.Equals("content-type", StringComparison.OrdinalIgnoreCase))
                continue;

            if (key.Equals("accept-encoding", StringComparison.OrdinalIgnoreCase))
                continue;

            if (key.Equals("referer", StringComparison.OrdinalIgnoreCase))
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri? referer))
                    requestHttp.Headers.Referrer = referer;

                continue;
            }

            requestHttp.Headers.TryAddWithoutValidation(key, value);
        }
    }
}
