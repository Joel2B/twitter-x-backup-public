using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;

namespace Backup.Infrastructure.Services.Config;

public sealed class JsonAppConfigStore : IAppConfigStore
{
    private readonly string? _configDirectory;

    public JsonAppConfigStore(string? configDirectory = null)
    {
        _configDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? ResolveDefaultDirectory()
            : Path.GetFullPath(configDirectory);
    }

    public AppConfig Load() => ConfigLoader.Load(_configDirectory);

    public DataConfig LoadData() => ConfigLoader.LoadData(_configDirectory);

    public void SaveData(DataConfig data) => ConfigLoader.SaveData(data, _configDirectory);

    private static string? ResolveDefaultDirectory()
    {
        List<string> candidates = [];
        string currentDirectory = Directory.GetCurrentDirectory();

        AddCandidates(candidates, AppContext.BaseDirectory);
        AddCandidates(candidates, currentDirectory);

        DirectoryInfo? cursor = new(currentDirectory);
        for (int depth = 0; depth < 8 && cursor is not null; depth++)
        {
            AddCandidates(candidates, cursor.FullName);
            cursor = cursor.Parent;
        }

        foreach (string candidate in candidates.Select(Path.GetFullPath).Distinct())
        {
            if (
                Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Services.json"))
            )
                return candidate;
        }

        return null;
    }

    private static void AddCandidates(List<string> candidates, string basePath)
    {
        candidates.Add(Path.Combine(basePath, "config"));
        candidates.Add(Path.Combine(basePath, "config.example"));
    }
}
