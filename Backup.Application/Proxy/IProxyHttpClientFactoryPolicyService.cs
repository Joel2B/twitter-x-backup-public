namespace Backup.Application.Proxy;

public interface IProxyHttpClientFactoryPolicyService
{
    HttpClientHandler CreateHandler(Uri? proxyUri);
    HttpClient CreateClient(HttpClientHandler handler, TimeSpan timeout);
}
