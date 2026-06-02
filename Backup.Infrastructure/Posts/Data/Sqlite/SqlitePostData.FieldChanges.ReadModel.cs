using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private List<Change> ToModelChanges(List<PostChangeEntity> entities, Post currentPost)
    {
        if (entities.Count == 0)
            return [];

        List<PostChangeReplayEntry> replayEntries = entities
            .OrderBy(entity => entity.Date)
            .ThenBy(entity => entity.Id)
            .Select(entity => new PostChangeReplayEntry
            {
                UserId = entity.UserId,
                Date = entity.Date,
                Sequence = entity.Id,
                Fields = entity
                    .Fields.OrderBy(field => field.Id)
                    .Select(field => new PostChangeReplayField
                    {
                        Field = field.Field,
                        OldValueJson = field.OldValueJson,
                    })
                    .ToList(),
            })
            .ToList();

        Backup.Domain.Posts.Post domainCurrent = PostReplicationMapper.ToDomain(currentPost);
        IReadOnlyList<Backup.Domain.Posts.Change> projected =
            _postChangeReadModelProjectionService.Project(domainCurrent, replayEntries);

        return projected.Select(PostReplicationMapper.ToAppChange).ToList();
    }
}
