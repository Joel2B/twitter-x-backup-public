namespace Backup.Application.Posts;

public static class PostResultResolutionPolicy
{
    public static T ResolvePrimaryThenRetweeted<T>(
        T source,
        Func<T, T?> getPrimary,
        Func<T, T?> getRetweeted
    )
        where T : class
    {
        T current = getPrimary(source) ?? source;
        T? retweeted = getRetweeted(current);

        if (retweeted is null)
            return current;

        return getPrimary(retweeted) ?? retweeted;
    }
}
