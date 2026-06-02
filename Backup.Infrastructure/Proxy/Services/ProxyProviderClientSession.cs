using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Services;

internal sealed class ProxyProviderClientSession(ProxyClientFactory proxyClientRotationService)
    : IDisposable
{
    private readonly ProxyClientFactory _proxyClientRotationService =
        proxyClientRotationService;
    private volatile HttpClient? _client;

    public HttpClient GetClient() =>
        _client ?? throw new InvalidOperationException();

    public void Rotate(IReadOnlyList<ProxyData> proxies)
    {
        HttpClient client = _proxyClientRotationService.CreateClient(proxies);
        HttpClient? oldClient = Interlocked.Exchange(ref _client, client);
        oldClient?.Dispose();
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
