using System.Text;
using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Infrastructure.Media.Models;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Media.Data;

public class LocalMediaCacheReader
{
    private static long? ReadNullableLong(
        JsonTextReader jr,
        IMediaCacheJsonSnapshotService snapshotService
    )
    {
        if (jr.TokenType == JsonToken.Null)
            return null;

        if (jr.TokenType is JsonToken.Integer or JsonToken.String)
            return snapshotService.ParseNullableLong(jr.Value);

        jr.Skip();

        return null;
    }

    public static async Task<MediaCacheSize?> ReadSizeAsync(
        JsonTextReader jr,
        IMediaCacheJsonSnapshotService snapshotService
    )
    {
        MediaCacheSize size = new();

        while (await jr.ReadAsync().ConfigureAwait(false))
        {
            if (jr.TokenType == JsonToken.EndObject)
                break;

            if (jr.TokenType != JsonToken.PropertyName)
            {
                jr.Skip();
                continue;
            }

            var name = (string)jr.Value!;

            if (!await jr.ReadAsync().ConfigureAwait(false))
                throw new JsonReaderException("JSON truncado dentro de Size.");

            switch (name)
            {
                case "Stream":
                    size.Stream = ReadNullableLong(jr, snapshotService);
                    break;

                case "File":
                    size.File = ReadNullableLong(jr, snapshotService);
                    break;

                default:
                    jr.Skip();
                    break;
            }
        }

        return size;
    }

    public static async IAsyncEnumerable<MediaCacheEntry> Get(
        string file,
        IMediaCacheJsonSnapshotService snapshotService,
        int bufferSize = 1 << 20
    )
    {
        await using FileStream fs = new(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            bufferSize: bufferSize,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan
        );

        using StreamReader sr = new(
            fs,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: bufferSize
        );

        using JsonTextReader jr = new(sr)
        {
            DateParseHandling = DateParseHandling.None,
            CloseInput = false,
        };

        while (await jr.ReadAsync().ConfigureAwait(false))
            if (jr.TokenType == JsonToken.StartArray)
                break;

        while (await jr.ReadAsync().ConfigureAwait(false))
        {
            if (jr.TokenType == JsonToken.StartObject)
            {
                string? pathVal = null;
                int? partitionIdl = null;
                MediaCacheSize? size = null;

                while (await jr.ReadAsync().ConfigureAwait(false))
                {
                    if (jr.TokenType == JsonToken.PropertyName)
                    {
                        var name = (string)jr.Value!;
                        if (!await jr.ReadAsync().ConfigureAwait(false))
                            break;

                        if (name.Equals("Path", StringComparison.OrdinalIgnoreCase))
                        {
                            pathVal =
                                jr.TokenType == JsonToken.String
                                    ? (string?)jr.Value
                                    : jr.Value?.ToString();
                        }
                        else if (name.Equals("Size", StringComparison.OrdinalIgnoreCase))
                        {
                            if (jr.TokenType == JsonToken.Null)
                                size = null;
                            else if (jr.TokenType == JsonToken.StartObject)
                                size = await ReadSizeAsync(jr, snapshotService)
                                    .ConfigureAwait(false);
                            else
                                jr.Skip();
                        }
                        else if (name.Equals("PartitionId", StringComparison.OrdinalIgnoreCase))
                        {
                            if (jr.TokenType == JsonToken.Null)
                                partitionIdl = null;
                            else if (jr.TokenType is JsonToken.Integer or JsonToken.String)
                                partitionIdl = snapshotService.ParseNullableInt(jr.Value);
                            else
                                jr.Skip();
                        }
                        else
                            jr.Skip();
                    }
                    else if (jr.TokenType == JsonToken.EndObject)
                    {
                        MediaCacheJsonSnapshot? snapshot = snapshotService.CreateSnapshot(
                            pathVal,
                            size?.Stream,
                            size?.File,
                            partitionIdl
                        );

                        if (snapshot is not null)
                            yield return new MediaCacheEntry
                            {
                                Path = snapshot.Path,
                                PartitionId = snapshot.PartitionId,
                                Size = new MediaCacheSize
                                {
                                    Stream = snapshot.StreamSizeBytes,
                                    File = snapshot.FileSizeBytes,
                                },
                            };
                        break;
                    }
                }
            }
            else if (jr.TokenType == JsonToken.EndArray)
            {
                yield break;
            }
        }
    }

    public static async Task Save(
        string file,
        List<MediaCacheEntry> lstCache,
        IMediaCacheJsonSnapshotService snapshotService,
        int bufferSize = 1 << 20
    )
    {
        string tmpPath = $"{file}.tmp";

        await Write(tmpPath, lstCache, snapshotService, bufferSize);

        if (OperatingSystem.IsWindows())
            File.Replace(tmpPath, file, null);
        else
            File.Move(tmpPath, file, true);
    }

    private static async Task Write(
        string file,
        List<MediaCacheEntry> lstCache,
        IMediaCacheJsonSnapshotService snapshotService,
        int bufferSize
    )
    {
        await using FileStream fs = new(
            file,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: bufferSize,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough
        );

        using StreamWriter sw = new(
            fs,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize
        );

        using JsonTextWriter jw = new(sw) { Formatting = Formatting.Indented };

        List<MediaCacheJsonSnapshot> snapshots = snapshotService
            .PrepareForWrite(
                lstCache.Select(cache => new MediaCacheJsonSnapshot
                {
                    Path = cache.Path,
                    StreamSizeBytes = cache.Size?.Stream,
                    FileSizeBytes = cache.Size?.File,
                    PartitionId = cache.PartitionId,
                })
            )
            .ToList();

        await jw.WriteStartArrayAsync().ConfigureAwait(false);

        foreach (MediaCacheJsonSnapshot snapshot in snapshots)
        {
            await jw.WriteStartObjectAsync().ConfigureAwait(false);

            await jw.WritePropertyNameAsync("Path").ConfigureAwait(false);
            await jw.WriteValueAsync(snapshot.Path).ConfigureAwait(false);

            await jw.WritePropertyNameAsync("Size").ConfigureAwait(false);

            if (snapshot.StreamSizeBytes is null && snapshot.FileSizeBytes is null)
                await jw.WriteNullAsync().ConfigureAwait(false);
            else
            {
                await jw.WriteStartObjectAsync().ConfigureAwait(false);

                await jw.WritePropertyNameAsync("Stream").ConfigureAwait(false);

                if (snapshot.StreamSizeBytes.HasValue)
                    await jw.WriteValueAsync(snapshot.StreamSizeBytes.Value).ConfigureAwait(false);
                else
                    await jw.WriteNullAsync().ConfigureAwait(false);

                await jw.WritePropertyNameAsync("File").ConfigureAwait(false);

                if (snapshot.FileSizeBytes.HasValue)
                    await jw.WriteValueAsync(snapshot.FileSizeBytes.Value).ConfigureAwait(false);
                else
                    await jw.WriteNullAsync().ConfigureAwait(false);

                await jw.WriteEndObjectAsync().ConfigureAwait(false);
            }

            await jw.WritePropertyNameAsync("PartitionId").ConfigureAwait(false);

            if (snapshot.PartitionId.HasValue)
                await jw.WriteValueAsync(snapshot.PartitionId.Value).ConfigureAwait(false);
            else
                await jw.WriteNullAsync().ConfigureAwait(false);

            await jw.WriteEndObjectAsync().ConfigureAwait(false);
        }

        await jw.WriteEndArrayAsync().ConfigureAwait(false);
        await jw.FlushAsync().ConfigureAwait(false);
        await sw.FlushAsync().ConfigureAwait(false);

        fs.Flush(true);
    }
}
