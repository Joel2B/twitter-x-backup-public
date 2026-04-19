using Backup.App.Models.Data.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private const string TablesCurrentDirectoryName = "current";
    private const string TablesCurrentOldDirectoryName = "current.old";
    private const string TablesTmpDirectoryName = "tmp";
    private const string LegacyDateFormat = "yyyy.MM.dd-HH.mm.ss";

    private const string NormalizedPostsFileName = "posts.json";
    private const string ProfilesFileName = "profiles.json";
    private const string HashtagsFileName = "hashtags.json";
    private const string MediasFileName = "medias.json";
    private const string MediaVariantsFileName = "media_variants.json";
    private const string IndexEntriesFileName = "index_entries.json";
    private const string PostChangesFileName = "post_changes.json";
    private const string PostChangeFieldsFileName = "post_change_fields.json";
    private const string PostMetaFileName = "post_meta.json";

    private sealed record TableManifestEntry(
        string FileName,
        Func<LocalPostTables, object> GetRows
    );

    private string GetTablesDirectoryPath(
        string directoryName,
        Models.Config.Data.Partition? partition = null
    )
    {
        Models.Config.Data.Partition target = partition ?? _partition.GetPrimary();
        return Path.Combine(GetPath(target), directoryName);
    }

    private string GetCurrentTablesFilePath(
        string fileName,
        Models.Config.Data.Partition? partition = null
    ) => Path.Combine(GetTablesDirectoryPath(TablesCurrentDirectoryName, partition), fileName);

    private void PrepareTablesDirectories(Models.Config.Data.Partition? partition = null)
    {
        string tmpPath = GetTablesDirectoryPath(TablesTmpDirectoryName, partition);
        string currentPath = GetTablesDirectoryPath(TablesCurrentDirectoryName, partition);
        string currentOldPath = GetTablesDirectoryPath(TablesCurrentOldDirectoryName, partition);

        if (Directory.Exists(tmpPath))
            Directory.Delete(tmpPath, recursive: true);

        if (!Directory.Exists(currentPath) && Directory.Exists(currentOldPath))
            Directory.Move(currentOldPath, currentPath);
    }

    private void ArchiveCurrentOldDirectory(Models.Config.Data.Partition? partition = null)
    {
        Models.Config.Data.Partition target = partition ?? _partition.GetPrimary();
        string currentOldPath = GetTablesDirectoryPath(TablesCurrentOldDirectoryName, target);

        if (!Directory.Exists(currentOldPath))
            return;

        string basePath = GetPath(target);
        string historyPath = GetUniqueHistoryDirectoryPath(basePath);

        Directory.Move(currentOldPath, historyPath);
    }

    private IEnumerable<string> GetDataFilePaths(Models.Config.Data.Partition? partition = null)
    {
        foreach (TableManifestEntry table in TableManifest)
            yield return GetCurrentTablesFilePath(table.FileName, partition);

        yield return GetCurrentTablesFilePath(PostMetaFileName, partition);
    }

    private async Task<LocalPostTables> LoadTables()
    {
        PrepareTablesDirectories();
        string postsPath = GetCurrentTablesFilePath(NormalizedPostsFileName);

        if (!File.Exists(postsPath))
            return new LocalPostTables();

        List<string> missingTables = TableManifest
            .Select(table => table.FileName)
            .Where(fileName => !File.Exists(GetCurrentTablesFilePath(fileName)))
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Select(fileName => fileName!)
            .ToList();

        if (missingTables.Count > 0)
            throw new Exception(
                $"Normalized current tables are incomplete. Missing files: {string.Join(", ", missingTables)}"
            );

        LocalPostTables tables = new();

        foreach (TableManifestEntry table in TableManifest)
            await LoadTable(tables, table.FileName);

        string postMetaPath = GetCurrentTablesFilePath(PostMetaFileName);

        if (!File.Exists(postMetaPath))
            throw new Exception(
                "Normalized current tables are incomplete. Missing file: post_meta.json"
            );

        tables.PostMeta = await ReadList<PostMetaRow>(postMetaPath);
        ValidatePostMetaConsistency(tables);

        return tables;
    }

    private static void ValidatePostMetaConsistency(LocalPostTables tables)
    {
        HashSet<string> postIds = tables
            .Posts.Select(row => row.Id)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> metaIds = tables
            .PostMeta.Where(row => !string.IsNullOrWhiteSpace(row.Id))
            .Select(row => row.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (postIds.SetEquals(metaIds))
            return;

        int missingInMeta = postIds.Count(id => !metaIds.Contains(id));
        int missingInPosts = metaIds.Count(id => !postIds.Contains(id));

        throw new Exception(
            $"post_meta is out of sync with posts. missingInMeta={missingInMeta}, missingInPosts={missingInPosts}"
        );
    }

    private async Task LoadTable(LocalPostTables tables, string fileName)
    {
        string path = GetCurrentTablesFilePath(fileName);

        switch (fileName)
        {
            case NormalizedPostsFileName:
            {
                string postsContent = await File.ReadAllTextAsync(path);
                tables.Posts =
                    JsonConvert.DeserializeObject<List<PostRow>>(postsContent)
                    ?? throw new Exception("Error deserializing posts file.");
                break;
            }
            case ProfilesFileName:
                tables.Profiles = await ReadList<ProfileRow>(path);
                break;
            case HashtagsFileName:
                tables.Hashtags = await ReadList<HashtagRow>(path);
                break;
            case MediasFileName:
                tables.Medias = await ReadList<MediaRow>(path);
                break;
            case MediaVariantsFileName:
                tables.MediaVariants = await ReadList<MediaVariantRow>(path);
                break;
            case IndexEntriesFileName:
                tables.IndexEntries = await ReadList<IndexEntryRow>(path);
                break;
            case PostChangesFileName:
                tables.PostChanges = await ReadList<PostChangeRow>(path);
                break;
            case PostChangeFieldsFileName:
                tables.PostChangeFields = await ReadList<PostChangeFieldRow>(path);
                break;
            default:
                throw new Exception($"Unknown table file '{fileName}'.");
        }
    }

    private async Task SaveTables(
        LocalPostTables tables,
        IReadOnlyDictionary<string, PostMetaRow> postMeta
    )
    {
        PrepareTablesDirectories();
        ArchiveCurrentOldDirectory();

        string tmpPath = GetTablesDirectoryPath(TablesTmpDirectoryName);
        string currentPath = GetTablesDirectoryPath(TablesCurrentDirectoryName);
        string currentOldPath = GetTablesDirectoryPath(TablesCurrentOldDirectoryName);

        Directory.CreateDirectory(tmpPath);
        await WriteTablesSnapshot(tables, postMeta, tmpPath);

        if (Directory.Exists(currentPath))
            Directory.Move(currentPath, currentOldPath);

        try
        {
            Directory.Move(tmpPath, currentPath);
        }
        catch
        {
            if (!Directory.Exists(currentPath) && Directory.Exists(currentOldPath))
                Directory.Move(currentOldPath, currentPath);

            throw;
        }

        ArchiveCurrentOldDirectory();
    }

    private async Task WriteTablesSnapshot(
        LocalPostTables tables,
        IReadOnlyDictionary<string, PostMetaRow> postMeta,
        string targetDirectoryPath
    )
    {
        string GetTargetFilePath(string fileName) => Path.Combine(targetDirectoryPath, fileName);

        async Task WriteTable(string fileName, object data, bool formatted)
        {
            _logger.LogInformation(
                "saving post table: {fileName}, formatted={formatted}",
                fileName,
                formatted
            );

            await WriteList(GetTargetFilePath(fileName), data, formatted);
        }

        foreach (bool formatted in new[] { false, true })
        {
            foreach (TableManifestEntry table in TableManifest)
                await WriteTable(table.FileName, table.GetRows(tables), formatted);

            List<PostMetaRow> postMetaRows = postMeta
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Value)
                .ToList();

            await WriteTable(PostMetaFileName, postMetaRows, formatted);
        }
    }
}
