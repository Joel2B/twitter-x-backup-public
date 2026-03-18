using System.Text.RegularExpressions;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Partition;
using Backup.App.Models.Bulk;
using Microsoft.Extensions.Logging;

namespace Backup.App.Data.Bulk;

public class LocalBulkSourceData(
    ILogger<LocalBulkSourceData> _logger,
    Models.Config.Data.Bulk.Storage _config,
    IPartition _partition
) : IBulkSourceData, ISetup
{
    private readonly ILogger<LocalBulkSourceData> _logger = _logger;
    private readonly Models.Config.Data.Bulk.Storage _config = _config;
    private readonly IPartition _partition = _partition;

    public Task Setup()
    {
        SetupDirectory();
        Replicate();

        return Task.CompletedTask;
    }

    private string GetPathSources(Models.Config.Data.Partition? partition = null)
    {
        Models.Config.Data.Partition primary = partition ?? _partition.GetPrimary();

        string path = Path.Combine(
            [.. primary.Paths, .. _config.Paths.Paths, .. _config.Paths.Sources.Paths]
        );

        return path;
    }

    private void SetupDirectory()
    {
        foreach (Models.Config.Data.Partition partition in _partition.GetPartitions())
            Directory.CreateDirectory(GetPathSources(partition));
    }

    public async Task<List<Source>> GetSources()
    {
        string path = GetPathSources();
        string[] files = Directory.GetFiles(path);

        Regex rx = new(
            @"(?<link>https?:\/\/(?:www\.)?x\.com\/(?<user>[^\/\s?#""'\\<>]+)(?:\/(?<type>[^\/\s?#""'\\<>]+))?(?:\/[^\s""'<>\\]*)?(?:\?[^\s""'<>\\#]*)?(?:\#[^\s""'<>\\]*)?)(?=[\s""'<>),;]|$)",
            RegexOptions.IgnoreCase
                | RegexOptions.Compiled
                | RegexOptions.Multiline
                | RegexOptions.ECMAScript
        );

        Dictionary<string, Source> data = [];

        foreach (string file in files)
        {
            await foreach (string line in File.ReadLinesAsync(file))
            {
                foreach (Match match in rx.Matches(line))
                {
                    if (!match.Success)
                        continue;

                    string link = match.Groups["link"].Value;
                    string user = match.Groups["user"].Value;
                    string type = match.Groups["type"].Value;

                    if (
                        string.IsNullOrEmpty(link)
                        || string.IsNullOrEmpty(user)
                        || string.IsNullOrEmpty(type)
                    )
                        continue;

                    Source source = new()
                    {
                        Link = link,
                        UserName = user,
                        Type = GetType(type),
                    };

                    data.TryAdd(user, source);
                }
            }
        }

        return [.. data.Values];
    }

    private static SourceType GetType(string type) =>
        type switch
        {
            "media" => SourceType.Media,
            "status" => SourceType.Status,
            _ => SourceType.None,
        };

    private void Replicate()
    {
        List<Models.Config.Data.Partition> partitions = _partition
            .GetPartitions()
            .Except([_partition.GetPrimary()])
            .ToList();

        string mainPath = GetPathSources();

        foreach (Models.Config.Data.Partition partition in partitions)
        {
            string path = GetPathSources(partition);

            foreach (string mainFile in Directory.EnumerateFiles(mainPath))
            {
                string file = Path.Combine(path, Path.GetFileName(mainFile));

                if (File.Exists(file))
                    continue;

                File.Copy(mainFile, file);
                _logger.LogInformation("{path} path copied", file);
            }
        }
    }
}
