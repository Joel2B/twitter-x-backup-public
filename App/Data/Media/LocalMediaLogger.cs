using System.Collections.Concurrent;
using System.Globalization;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Media.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Media;

public class LocalMediaLogger(
    ILogger<LocalMediaLogger> _logger,
    Models.Config.App config,
    IPartition _partition
) : IMediaLogger, ISetup
{
    private readonly ILogger<LocalMediaLogger> _logger = _logger;
    public readonly Models.Config.App _config = config;
    private readonly IPartition _partition = _partition;

    private readonly ConcurrentDictionary<string, Logs> _errors = new();
    private readonly ConcurrentDictionary<string, Logs> _logs = new();

    public Task Setup()
    {
        SetupDirectory();

        return Task.CompletedTask;
    }

    public void SetupDirectory()
    {
        Directory.CreateDirectory(GetPath());
        Directory.CreateDirectory(GetPathLog());
        Directory.CreateDirectory(GetPathError());
    }

    private string GetPath()
    {
        Models.Config.Data.Partition partition = _partition
            .GetPartitions(_config.Downloads.Media.Partitions)
            .First();

        return Path.Combine([.. partition.Paths, .. _config.Downloads.Media.Paths]);
    }

    private string GetPathLog() => Path.Combine([GetPath(), .. _config.Downloads.Media.Log.Paths]);

    private string GetPathError() =>
        Path.Combine([GetPath(), .. _config.Downloads.Media.Error.Paths]);

    public void Log(Logs log)
    {
        Logs UpdateFunc(string key, Logs _log)
        {
            lock (_log.Messages)
                _log.Messages.Add(log.Messages[0]);

            return _log;
        }

        _logs.AddOrUpdate(log.Id, log, UpdateFunc);

        _logger.LogInformation(
            "{logId}, {messageId}, {message}",
            log.Id,
            log.Messages[0].Id,
            log.Messages[0].Message
        );
    }

    public void Error(Logs log, bool logger = true)
    {
        Logs UpdateFunc(string key, Logs _log)
        {
            lock (_log.Messages)
                _log.Messages.Add(log.Messages[0]);

            return _log;
        }

        _errors.AddOrUpdate(log.Id, log, UpdateFunc);

        if (!logger)
            return;

        _logger.LogError(
            "{logId}, {messageId}, {message}",
            log.Id,
            log.Messages[0].Id,
            log.Messages[0].Message
        );
    }

    private static async Task SaveFile(List<Logs> logs, string path, bool force = false)
    {
        if (logs.Count == 0 && !force)
            return;

        string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
        string fileName = $"{date}.json";
        string _path = Path.Combine(path, fileName);

        string log = JsonConvert.SerializeObject(logs, Formatting.Indented);
        await File.WriteAllTextAsync(_path, log);
    }

    public async Task Save()
    {
        await SaveFile([.. _logs.Values], GetPathLog());
        await SaveFile([.. _errors.Values], GetPathError());
    }

    public async Task SaveErrors(List<Logs> logs)
    {
        await SaveFile(logs, GetPathError(), true);
    }

    public async Task<List<Logs>?> GetErrors()
    {
        string[] paths = Directory.GetFiles(GetPathError(), "*.json");

        LogFile? lastLogFile = paths
            .Select(path => new LogFile
            {
                Path = path,
                Date = DateTime.ParseExact(
                    Path.GetFileNameWithoutExtension(path),
                    "yyyy.MM.dd-HH.mm.ss",
                    CultureInfo.InvariantCulture
                ),
            })
            .OrderByDescending(file => file.Date)
            .FirstOrDefault();

        if (lastLogFile is null)
            return null;

        string content = await File.ReadAllTextAsync(lastLogFile.Path);
        List<Logs>? errors = JsonConvert.DeserializeObject<List<Logs>>(content);

        if (errors is null)
            throw new Exception("Error in deserialize");

        if (errors.Count == 0)
            return null;

        return errors;
    }

    public Task<List<Logs>> GetMemoryErrors() => Task.FromResult<List<Logs>>([.. _errors.Values]);
}
