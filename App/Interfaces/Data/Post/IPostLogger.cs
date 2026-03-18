namespace Backup.App.Interfaces.Data.Post;

public interface IPostLogger
{
    public Task Save(string data, CancellationToken token);
    public Task Prune();
}
