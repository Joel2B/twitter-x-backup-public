using Backup.App.Models.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    public async Task<Dictionary<string, Models.Post.Post>> AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        MergeOptions? options = null
    )
    {
        options ??= new();

        Dictionary<string, Models.Post.Post> posts = await GetCache() ?? [];
        _postsCache ??= posts;

        foreach (Models.Post.Post result in incoming)
        {
            if (!posts.TryGetValue(result.Id, out Models.Post.Post? post))
            {
                posts[result.Id] = result;
                continue;
            }

            Change change = new() { UserId = userId };
            MergeData(post, result, change);

            if (
                !result.Index.TryGetValue(
                    userId,
                    out Dictionary<string, IndexData>? incomingUserIndex
                )
            )
            {
                incomingUserIndex = [];
                result.Index[userId] = incomingUserIndex;
            }

            if (!incomingUserIndex.ContainsKey(origin))
                incomingUserIndex[origin] = new();

            IndexData newIndexData = incomingUserIndex[origin].Clone();
            result.Index = post.CloneIndex();

            if (options.Index)
            {
                if (!result.Index.ContainsKey(userId))
                    result.Index[userId] = [];

                result.Index[userId][origin] = newIndexData;
                MergeIndex(post, result, change, origin);
            }

            result.Changes = post.CloneChanges();

            if (change.Data is not null || change.Index is not null)
                result.Changes.Add(change);

            posts[post.Id] = result;
        }

        return posts;
    }

    private void MergeData(Models.Post.Post post, Models.Post.Post result, Change change)
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

        Extensions.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old data",
            "new data",
            post.Clone(),
            result.Clone()
        );
    }

    private void MergeIndex(
        Models.Post.Post post,
        Models.Post.Post result,
        Change change,
        string origin
    )
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

        Extensions.LoggingExtensions.LogAsJsonDiff(
            _logger,
            "old index",
            "new index",
            post.Index[change.UserId],
            result.Index[change.UserId]
        );
    }
}
