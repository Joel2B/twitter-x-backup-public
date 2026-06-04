using Newtonsoft.Json;

namespace Backup.Infrastructure.Logging;

public static class HttpHeaderSanitizer
{
    private static readonly HashSet<string> FullyRedactedHeaders = new(
        [
            "authorization",
            "proxy-authorization",
            "cookie",
            "set-cookie",
            "x-csrf-token",
            "x-guest-token",
            "x-twitter-auth-type",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    public static IReadOnlyDictionary<string, string> Sanitize(
        IReadOnlyDictionary<string, string>? headers
    )
    {
        if (headers is null || headers.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string> sanitized = new(headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((string key, string value) in headers)
            sanitized[key] = SanitizeHeaderValue(key, value);

        return sanitized;
    }

    public static string ToSanitizedJson(IReadOnlyDictionary<string, string>? headers)
    {
        IReadOnlyDictionary<string, string> sanitized = Sanitize(headers);
        return JsonConvert.SerializeObject(sanitized, Formatting.None);
    }

    private static string SanitizeHeaderValue(string key, string value)
    {
        if (FullyRedactedHeaders.Contains(key))
            return "[REDACTED]";

        return value;
    }
}
