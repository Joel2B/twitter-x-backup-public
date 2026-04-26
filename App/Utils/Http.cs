namespace Backup.App.Utils;

public class Http
{
    public static void ApplyHeaders(
        HttpRequestMessage requestHttp,
        IReadOnlyDictionary<string, string> headers
    )
    {
        foreach (KeyValuePair<string, string> kvp in headers)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            string key = kvp.Key.Trim();
            string value = kvp.Value.Trim();

            if (key.Equals("content-type", StringComparison.OrdinalIgnoreCase))
            {
                // GET requests do not need Content-Type; ignore to avoid invalid header placement.
                continue;
            }

            if (key.Equals("accept-encoding", StringComparison.OrdinalIgnoreCase))
            {
                // Let HttpClient advertise only encodings it can decompress automatically.
                continue;
            }

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
