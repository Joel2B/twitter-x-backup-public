namespace Backup.App.Models.Config;

public static class FetchContextFactory
{
    public static FetchContext Create(Source current, Source source)
    {
        Source merged = current.Clone();

        merged.Id = source.Id;
        merged.Enabled = source.Enabled;
        merged.Count = source.Count;

        Request.RequestMerge.MergeInto(merged.Request, source.Request);

        return new() { Source = merged };
    }
}
