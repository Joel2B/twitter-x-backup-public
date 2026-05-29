namespace Backup.App.Interfaces.Proxy;

public interface IProxyProvider
{
    public HttpClient GetClient();
    public Task Next(CancellationToken token);
    public Task Reset();
    public void OnUse();
    public void OnError(Exception ex);
    public Task SaveData();
}
