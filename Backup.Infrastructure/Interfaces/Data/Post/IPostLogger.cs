namespace Backup.Infrastructure.Interfaces.Data.Posts;

public interface IPostLogger
{
    public Task Save(string sourceId, string data, CancellationToken token);
    public Task Prune();
}

