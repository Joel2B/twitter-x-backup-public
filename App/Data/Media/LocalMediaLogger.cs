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

    public async Task Setup()
    {
        SetupDirectory();
        await LoadErrors();
    }

    private void SetupDirectory()
    {
        Directory.CreateDirectory(GetPath());
        Directory.CreateDirectory(GetPathLog());
        Directory.CreateDirectory(GetPathError());
    }

    private async Task LoadErrors()
    {
        List<Logs> lstLogs = await ReadErrors() ?? [];

        foreach (Logs logs in lstLogs)
            if (!_errors.TryAdd(logs.Id, logs))
                throw new Exception();
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

    private static async Task SaveFile(List<Logs> logs, string path)
    {
        if (logs.Count == 0)
            return;

        string date = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
        string fileName = $"{date}.json";
        string _path = Path.Combine(path, fileName);

        string log = JsonConvert.SerializeObject(logs, Formatting.Indented);
        await File.WriteAllTextAsync(_path, log);
    }

    private async Task<List<Logs>?> ReadErrors()
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

    private async Task SaveErrors() => await SaveFile([.. _errors.Values], GetPathError());

    public Task<List<Logs>?> GetErrors() => Task.FromResult<List<Logs>?>([.. _errors.Values]);

    public async Task RemoveErrors(List<Logs> lstLogs)
    {
        foreach (Logs logs in lstLogs)
            if (!_errors.TryRemove(logs.Id, out _))
                throw new Exception();

        await SaveErrors();
    }

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

    public void Error(Logs log)
    {
        Logs UpdateFunc(string key, Logs _log)
        {
            lock (_log.Messages)
                _log.Messages.Add(log.Messages[0]);

            return _log;
        }

        _errors.AddOrUpdate(log.Id, log, UpdateFunc);

        _logger.LogError(
            "{logId}, {messageId}, {message}",
            log.Id,
            log.Messages[0].Id,
            log.Messages[0].Message
        );
    }

    public async Task Save()
    {
        await SaveFile([.. _logs.Values], GetPathLog());
        await SaveErrors();
    }
}
