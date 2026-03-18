using System.Text;
using Backup.App.Models.Media;
using Newtonsoft.Json;

namespace Backup.App.Data.Media;

public class LocalMediaCacheReader
{
    public static long? ReadNullableLong(JsonTextReader jr)
    {
        if (jr.TokenType == JsonToken.Null)
            return null;
        if (jr.TokenType == JsonToken.Integer)
            return Convert.ToInt64(jr.Value);
        if (jr.TokenType == JsonToken.String && long.TryParse((string)jr.Value!, out var v))
            return v;

        jr.Skip();

        return null;
    }

    public static async Task<Size?> ReadSizeAsync(JsonTextReader jr)
    {
        Size size = new();

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
                    size.Stream = ReadNullableLong(jr);
                    break;

                case "File":
                    size.File = ReadNullableLong(jr);
                    break;

                default:
                    jr.Skip();
                    break;
            }
        }

        return size;
    }

    public static async IAsyncEnumerable<Cache> Get(string file, int bufferSize = 1 << 20)
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
                Size? size = null;

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
                                size = await ReadSizeAsync(jr).ConfigureAwait(false);
                            else
                                jr.Skip();
                        }
                        else if (name.Equals("PartitionId", StringComparison.OrdinalIgnoreCase))
                        {
                            if (jr.TokenType == JsonToken.Integer)
                                partitionIdl = Convert.ToInt32(jr.Value);
                            else if (
                                jr.TokenType == JsonToken.String
                                && int.TryParse((string)jr.Value!, out var l)
                            )
                                partitionIdl = l;
                            else if (jr.TokenType == JsonToken.Null)
                                partitionIdl = null;
                            else
                                jr.Skip();
                        }
                        else
                            jr.Skip();
                    }
                    else if (jr.TokenType == JsonToken.EndObject)
                    {
                        if (pathVal != null)
                            yield return new Cache
                            {
                                Path = pathVal,
                                PartitionId = partitionIdl,
                                Size = size,
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

    public static async Task Save(string file, List<Cache> lstCache, int bufferSize = 1 << 20)
    {
        string tmpPath = $"{file}.tmp";

        await Write(tmpPath, lstCache, bufferSize);

        if (OperatingSystem.IsWindows())
            File.Replace(tmpPath, file, null);
        else
            File.Move(tmpPath, file, true);
    }

    private static async Task Write(string file, List<Cache> lstCache, int bufferSize)
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

        await jw.WriteStartArrayAsync().ConfigureAwait(false);

        foreach (Cache cache in lstCache.ToArray())
        {
            await jw.WriteStartObjectAsync().ConfigureAwait(false);

            await jw.WritePropertyNameAsync("Path").ConfigureAwait(false);
            await jw.WriteValueAsync(cache.Path).ConfigureAwait(false);

            await jw.WritePropertyNameAsync("Size").ConfigureAwait(false);

            if (cache.Size is null)
                await jw.WriteNullAsync().ConfigureAwait(false);
            else
            {
                await jw.WriteStartObjectAsync().ConfigureAwait(false);

                await jw.WritePropertyNameAsync("Stream").ConfigureAwait(false);

                if (cache.Size.Stream.HasValue)
                    await jw.WriteValueAsync(cache.Size.Stream.Value).ConfigureAwait(false);
                else
                    await jw.WriteNullAsync().ConfigureAwait(false);

                await jw.WritePropertyNameAsync("File").ConfigureAwait(false);

                if (cache.Size.File.HasValue)
                    await jw.WriteValueAsync(cache.Size.File.Value).ConfigureAwait(false);
                else
                    await jw.WriteNullAsync().ConfigureAwait(false);

                await jw.WriteEndObjectAsync().ConfigureAwait(false);
            }

            await jw.WritePropertyNameAsync("PartitionId").ConfigureAwait(false);

            if (cache.PartitionId.HasValue)
                await jw.WriteValueAsync(cache.PartitionId.Value).ConfigureAwait(false);
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
