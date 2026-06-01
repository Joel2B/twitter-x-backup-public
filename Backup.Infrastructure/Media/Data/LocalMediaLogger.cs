using System.Collections.Concurrent;
using Backup.Application.Core;
using Backup.Application.IO;
using Backup.Application.Media;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Logging;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaLogger(
    ILogger<LocalMediaLogger> _logger,
    AppConfig config,
    IPartition _partition,
    IMediaStoragePathService mediaStoragePathService,
    IMediaLogFilePolicyService mediaLogFilePolicyService,
    IDataStoreGuardService dataStoreGuardService,
    IDateTimeProvider dateTimeProvider
) : IMediaLogger, ISetup
{
    private readonly ILogger<LocalMediaLogger> _logger = _logger;
    public readonly AppConfig _config = config;
    private readonly IPartition _partition = _partition;
    private readonly IMediaStoragePathService _mediaStoragePathService = mediaStoragePathService;
    private readonly IMediaLogFilePolicyService _mediaLogFilePolicyService =
        mediaLogFilePolicyService;
    private readonly IDataStoreGuardService _dataStoreGuardService = dataStoreGuardService;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

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
        return _mediaStoragePathService.BuildMediaRootPath(
            _partition
                .GetPartitions(_config.Downloads.Media.Partitions)
                .Select(partition => Path.Combine([.. partition.Paths])),
            _config.Downloads.Media.Paths
        );
    }

    private string GetPathLog() =>
        _mediaStoragePathService.BuildMediaLogPath(GetPath(), _config.Downloads.Media.Log.Paths);

    private string GetPathError() =>
        _mediaStoragePathService.BuildMediaErrorPath(
            GetPath(),
            _config.Downloads.Media.Error.Paths
        );

    private async Task SaveFile(List<Logs> logs, string path)
    {
        if (logs.Count == 0)
            return;

        string fileName = _mediaLogFilePolicyService.CreateFileName(_dateTimeProvider.Now);
        string _path = Path.Combine(path, fileName);

        string log = JsonConvert.SerializeObject(logs, Formatting.Indented);
        await File.WriteAllTextAsync(_path, log);
    }

    private async Task<List<Logs>?> ReadErrors()
    {
        string[] paths = Directory.GetFiles(GetPathError(), "*.json");
        string? lastLogPath = _mediaLogFilePolicyService.SelectLatestFilePath(paths);

        if (string.IsNullOrWhiteSpace(lastLogPath))
            return null;

        string content = await File.ReadAllTextAsync(lastLogPath);
        List<Logs>? deserialized = JsonConvert.DeserializeObject<List<Logs>>(content);
        List<Logs> errors = _dataStoreGuardService.RequireDeserialized(
            deserialized,
            "Error in deserialize"
        );

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
