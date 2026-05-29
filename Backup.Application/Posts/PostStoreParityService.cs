using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public class PostStoreParityService : IPostStoreParityService
{
    public async Task<PostStoreParityResult> Verify(IEnumerable<IPostStoreCountSource> stores)
    {
        List<IPostStoreCountSource> storeList = [.. stores];

        if (storeList.Count <= 1)
        {
            return new PostStoreParityResult
            {
                PrimaryLabel = storeList.FirstOrDefault()?.Label ?? "",
                Snapshots = []
            };
        }

        List<IPostStoreCountSource> defaults = storeList.Where(store => store.IsDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException("Only one post data store can be marked as default.");

        IPostStoreCountSource primary = defaults.FirstOrDefault() ?? storeList.First();
        Dictionary<IPostStoreCountSource, PostStoreCounts> countsByStore = [];

        foreach (IPostStoreCountSource store in storeList)
            countsByStore[store] = await store.GetStoreCounts();

        PostStoreCounts primaryCounts = countsByStore[primary];
        List<PostStoreMismatch> mismatches = [];

        foreach (IPostStoreCountSource secondary in storeList.Where(store => !ReferenceEquals(store, primary)))
        {
            PostStoreCounts secondaryCounts = countsByStore[secondary];
            List<string> diffs = GetCountDiffs(primaryCounts, secondaryCounts);

            if (diffs.Count == 0)
                continue;

            mismatches.Add(
                new PostStoreMismatch
                {
                    PrimaryLabel = primary.Label,
                    SecondaryLabel = secondary.Label,
                    Diffs = diffs
                }
            );
        }

        return new PostStoreParityResult
        {
            PrimaryLabel = primary.Label,
            Snapshots = storeList
                .Select(store => new PostStoreSnapshot
                {
                    Label = store.Label,
                    Counts = countsByStore[store]
                })
                .ToList(),
            Mismatches = mismatches
        };
    }

    private static List<string> GetCountDiffs(PostStoreCounts left, PostStoreCounts right)
    {
        List<string> diffs = [];
        AddDiff("posts", left.Posts, right.Posts);
        AddDiff("profiles", left.Profiles, right.Profiles);
        AddDiff("hashtags", left.Hashtags, right.Hashtags);
        AddDiff("medias", left.Medias, right.Medias);
        AddDiff("mediaVariants", left.MediaVariants, right.MediaVariants);
        AddDiff("indexEntries", left.IndexEntries, right.IndexEntries);
        AddDiff("changes", left.Changes, right.Changes);
        AddDiff("changeFields", left.ChangeFields, right.ChangeFields);
        AddDiff("hashMeta", left.HashMeta, right.HashMeta);

        return diffs;

        void AddDiff(string name, int a, int b)
        {
            if (a == b)
                return;

            diffs.Add($"{name}:{a}!={b}");
        }
    }
}

