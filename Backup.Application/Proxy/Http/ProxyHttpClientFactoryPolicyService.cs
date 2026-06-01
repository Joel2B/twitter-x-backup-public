using System.Net;

namespace Backup.Application.Proxy;

public sealed class ProxyHttpClientFactoryPolicyService : IProxyHttpClientFactoryPolicyService
{
    public HttpClientHandler CreateHandler(Uri? proxyUri)
    {
        HttpClientHandler handler = new();

        if (proxyUri is null)
            return handler;

        handler.Proxy = new WebProxy(proxyUri);
        handler.UseProxy = true;
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        return handler;
    }

    public HttpClient CreateClient(HttpClientHandler handler, TimeSpan timeout) =>
        new(handler, disposeHandler: true) { Timeout = timeout };
}
