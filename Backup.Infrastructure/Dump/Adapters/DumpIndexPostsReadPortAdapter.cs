using Backup.Application.Dump.Ports;
using Backup.Application.IO;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Dump.Adapters;

public sealed class DumpIndexPostsReadPortAdapter(IDataStoreGuardService dataStoreGuardService)
    : IDumpIndexPostsReadPort
{
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;

    public async Task<IReadOnlyList<Backup.Domain.Posts.Post>> ReadPosts(string path)
    {
        string content = await File.ReadAllTextAsync(path);
        List<Post>? deserialized = JsonConvert.DeserializeObject<List<Post>>(content);
        List<Post> posts = _dataStoreGuardService.RequireDeserialized(deserialized, "Error in deserialize");

        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }
}
