using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Adapters;

internal static class PostMergeDiagnosticsLogger
{
    public static void LogDataChange(
        ILogger logger,
        Post current,
        Post merged,
        string userId,
        object oldData,
        object newData
    )
    {
        logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            current.Id,
            userId,
            merged.Profile.UserName
        );

        Logging.LoggingExtensions.LogAsJsonDiff(logger, "old data", "new data", oldData, newData);
    }

    public static void TryLogIndexChange(
        ILogger logger,
        Post current,
        Post merged,
        string userId,
        string? origin = null
    )
    {
        if (!current.Index.TryGetValue(userId, out Dictionary<string, IndexData>? oldUserIndex))
            return;

        if (!merged.Index.TryGetValue(userId, out Dictionary<string, IndexData>? newUserIndex))
            return;

        if (origin is not null)
        {
            if (!oldUserIndex.ContainsKey(origin))
                return;

            if (!newUserIndex.ContainsKey(origin))
                return;
        }

        logger.LogInformation(
            "id: {id}, userId: {userId}, userName: {userName}",
            current.Id,
            userId,
            merged.Profile.UserName
        );

        Logging.LoggingExtensions.LogAsJsonDiff(
            logger,
            "old index",
            "new index",
            oldUserIndex,
            newUserIndex
        );
    }
}
