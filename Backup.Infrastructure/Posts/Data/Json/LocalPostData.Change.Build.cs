using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private void AddChangeRows(LocalPostTables tables, Post post)
    {
        Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(post);
        IReadOnlyList<PostComputedChange> computedChanges = _postChangeComputationService.Compute(
            domainPost
        );

        for (int changeOrdinal = 0; changeOrdinal < computedChanges.Count; changeOrdinal++)
        {
            PostComputedChange change = computedChanges[changeOrdinal];

            tables.PostChanges.Add(
                new PostChangeRow
                {
                    PostId = post.Id,
                    Ordinal = changeOrdinal,
                    UserId = change.UserId,
                    Date = change.Date,
                    ChangeType = change.ChangeType,
                }
            );

            for (int fieldOrdinal = 0; fieldOrdinal < change.Fields.Count; fieldOrdinal++)
            {
                PostComputedChangeField field = change.Fields[fieldOrdinal];

                tables.PostChangeFields.Add(
                    new PostChangeFieldRow
                    {
                        PostId = post.Id,
                        ChangeOrdinal = changeOrdinal,
                        Ordinal = fieldOrdinal,
                        Field = field.Field,
                        OldValueJson = field.OldValueJson,
                        NewValueJson = field.NewValueJson,
                    }
                );
            }
        }
    }
}
