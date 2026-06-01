using System.Net.Http.Headers;

namespace Backup.Application.Proxy;

public sealed class ProxyHttpClientHeaderPolicyService : IProxyHttpClientHeaderPolicyService
{
    public void Apply(HttpRequestHeaders headers)
    {
        headers.UserAgent.Clear();
        headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36"
        );

        headers.Accept.Clear();
        headers.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"
        );

        headers.AcceptLanguage.Clear();
        headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        headers.TryAddWithoutValidation("Priority", "u=0, i");
        headers.TryAddWithoutValidation(
            "Sec-ch-ua",
            "\"Brave\";v=\"143\", \"Chromium\";v=\"143\", \"Not A(Brand\";v=\"24\""
        );

        headers.TryAddWithoutValidation("Sec-ch-ua-mobile", "?0");
        headers.TryAddWithoutValidation("Sec-ch-ua-platform", "Windows");
        headers.TryAddWithoutValidation("Sec-fetch-dest", "document");
        headers.TryAddWithoutValidation("Sec-fetch-mode", "navigate");
        headers.TryAddWithoutValidation("Sec-fetch-site", "none");
        headers.TryAddWithoutValidation("Sec-fetch-user", "?1");
        headers.TryAddWithoutValidation("Sec-gpc", "1");
    }
}
