using System.Net.Http.Headers;

namespace Backup.Application.Proxy;

public interface IProxyHttpClientHeaderPolicyService
{
    void Apply(HttpRequestHeaders headers);
}
