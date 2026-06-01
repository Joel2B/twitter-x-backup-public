using Backup.Application.Dump.Ports;

namespace Backup.Application.Dump;

public sealed class DumpIndexLoadService(
    IDumpIndexFilePolicyService dumpIndexFilePolicyService,
    IDumpIndexPostsReadPort dumpIndexPostsReadPort
) : IDumpIndexLoadService
{
    private readonly IDumpIndexFilePolicyService _dumpIndexFilePolicyService =
        dumpIndexFilePolicyService;
    private readonly IDumpIndexPostsReadPort _dumpIndexPostsReadPort = dumpIndexPostsReadPort;

    public async Task<IReadOnlyList<Backup.Domain.Posts.Post>> LoadPosts(
        IReadOnlyList<string> allPaths,
        IReadOnlyList<string> apiPathSegments
    )
    {
        IReadOnlyList<string> indexPaths = _dumpIndexFilePolicyService.SelectIndexFiles(
            allPaths,
            apiPathSegments
        );

        List<Backup.Domain.Posts.Post> posts = [];

        foreach (string path in indexPaths)
        {
            IReadOnlyList<Backup.Domain.Posts.Post> readPosts =
                await _dumpIndexPostsReadPort.ReadPosts(path);
            posts.AddRange(readPosts);
        }

        return posts;
    }
}
