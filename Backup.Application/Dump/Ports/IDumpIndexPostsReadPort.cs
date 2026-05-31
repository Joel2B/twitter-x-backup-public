namespace Backup.Application.Dump.Ports;

public interface IDumpIndexPostsReadPort
{
    Task<IReadOnlyList<Backup.Domain.Posts.Post>> ReadPosts(string path);
}
