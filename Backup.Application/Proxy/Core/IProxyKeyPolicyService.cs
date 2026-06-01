namespace Backup.Application.Proxy;

public interface IProxyKeyPolicyService
{
    string Build(string ip, string port, string protocol);
}
