namespace Backup.Application.Network;

public interface IHttpRequestHeaderPolicyService
{
    void ApplyHeaders(HttpRequestMessage requestHttp, IReadOnlyDictionary<string, string> headers);
}
