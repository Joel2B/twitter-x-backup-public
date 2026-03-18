namespace Backup.App.Interfaces.Proxy;

public interface IProxyFormatter
{
    public List<Models.Proxy.Proxy>? Load(List<string> lines, string protocol);
}
