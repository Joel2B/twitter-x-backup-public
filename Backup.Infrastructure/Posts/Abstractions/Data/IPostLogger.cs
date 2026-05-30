namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostLogger
{
    public Task Save(string sourceId, string data, CancellationToken token);
    public Task Prune();
}
