using Backup.Infrastructure.Proxy.Models;
using Backup.Infrastructure.Proxy.Abstractions.Core;

namespace Backup.Infrastructure.Proxy.Services;

internal sealed class ProxyProviderClientSession(IProxyClientRotationService proxyClientRotationService)
    : IDisposable
{
    private readonly IProxyClientRotationService _proxyClientRotationService = proxyClientRotationService;
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
