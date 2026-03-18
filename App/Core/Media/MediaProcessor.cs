using Backup.App.Models.Media;

namespace Backup.App.Core.Media;

public abstract class MediaProcessor(MediaProcessorContext context)
{
    protected MediaProcessorContext Context = context;

    private readonly Dictionary<string, Download> _logs = [];

    public abstract void Process();

    public void AddDataDownload(string id, DataDownload data, bool include)
    {
        Download all = GetOrCreate(Context.All, id);
        all.Data.Add(data);

        if (!include)
            return;

        Download filtered = GetOrCreate(Context.Filtered, id);
        filtered.Data.Add(data.Clone());

        Download logs = GetOrCreate(_logs, id);
        logs.Data.Add(data.Clone());
    }

    private static Download GetOrCreate(Dictionary<string, Download> downloads, string id)
    {
        if (!downloads.TryGetValue(id, out Download? dl))
        {
            dl = new() { Id = id, Data = [] };
            downloads[id] = dl;
        }

        return dl;
    }

    public void FilterDuplicates()
    {
        Filter(Context.All);
        Filter(Context.Filtered);
        Filter(_logs);
    }

    private static void Filter(Dictionary<string, Download> downloads)
    {
        HashSet<string> urls = new(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in downloads.ToArray())
        {
            Download download = downloads[kv.Key];
            download.Data = [.. download.Data.Where(data => urls.Add(data.Url))];

            if (download.Data.Count == 0)
                downloads.Remove(kv.Key);
        }
    }

    public async Task SaveLog()
    {
        string type = GetType().Name.Replace("Processor", "").ToLowerInvariant() + "s";
        await Context.logger.Save(type, [.. _logs.Values]);
    }
}
