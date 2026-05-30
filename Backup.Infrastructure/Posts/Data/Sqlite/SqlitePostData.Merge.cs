using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

public partial class SqlitePostData
{
    private void MergeData(Post post, Post result, Change change)
    {
        result.Profile.UserName ??= post.Profile.UserName;
        result.Profile.Name ??= post.Profile.Name;
        result.Profile.BannerUrl ??= post.Profile.BannerUrl;
        result.Profile.ImageUrl ??= post.Profile.ImageUrl;
        result.Profile.Following ??= post.Profile.Following;

        if (post.Equals(result))
            return;

        change.Data = post.Clone();

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            post.Clone(),
            result.Clone()
        );
    }

    private void MergeIndex(Post post, Post result, Change change, string origin)
    {
        if (!post.Index.ContainsKey(change.UserId))
            post.Index[change.UserId] = [];

        if (!post.Index[change.UserId].TryGetValue(origin, out IndexData? indexPost))
        {
            indexPost = new();
            post.Index[change.UserId][origin] = indexPost;
        }

        IndexData indexResult = result.Index[change.UserId][origin];

        if (
            indexPost.Previous is null
            || indexPost.Next is null
            || indexResult.Previous is null
            || indexResult.Next is null
            || indexPost.Equals(indexResult)
        )
            return;

        change.Index = post.CloneIndex()[change.UserId];

        _logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            post.Id,
            change.UserId,
            result.Profile.UserName
        );

        Backup.Infrastructure.Logging.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            post.Index[change.UserId],
            result.Index[change.UserId]
        );
    }
}
