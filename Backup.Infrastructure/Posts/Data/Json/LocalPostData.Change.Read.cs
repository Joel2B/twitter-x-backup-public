using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private List<Change> ToModelChanges(
        List<PostChangeRow> changeRows,
        Dictionary<(string PostId, int ChangeOrdinal), List<PostChangeFieldRow>> fieldsByChange,
        Post currentPost
    )
    {
        if (changeRows.Count == 0)
            return [];

        List<PostChangeReplayEntry> replayEntries = changeRows
            .OrderBy(o => o.Date)
            .ThenBy(o => o.Ordinal)
            .Select(changeRow =>
            {
                fieldsByChange.TryGetValue(
                    (changeRow.PostId, changeRow.Ordinal),
                    out List<PostChangeFieldRow>? fields
                );
                fields ??= [];

                return new PostChangeReplayEntry
                {
                    UserId = changeRow.UserId,
                    Date = changeRow.Date,
                    Sequence = changeRow.Ordinal,
                    Fields = fields
                        .Select(field => new PostChangeReplayField
                        {
                            Field = field.Field,
                            OldValueJson = field.OldValueJson,
                        })
                        .ToList(),
                };
            })
            .ToList();

        Backup.Domain.Posts.Post domainCurrent = PostReplicationMapper.ToDomain(currentPost);
        IReadOnlyList<Backup.Domain.Posts.Change> projected =
            _postChangeReadModelProjectionService.Project(domainCurrent, replayEntries);

        return projected.Select(PostReplicationMapper.ToAppChange).ToList();
    }
}
