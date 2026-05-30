using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaDownloadDataBuilderService : IMediaDownloadDataBuilderService
{
    public MediaDownloadData Build(MediaDownloadDataBuildInput input)
    {
        List<string?> segments =
        [
            input.PostId,
            input.MediaType,
            input.Id,
            .. input.MidPath,
            input.FormatType,
            input.ResolutionType,
            $"{input.Name}.{input.FormatType}",
        ];

        string path = Path.Combine([.. segments.OfType<string>()]);
        string url = input.Url;

        if (input.IncludeQuery)
        {
            url = AddQuery(url, "format", input.FormatType);
            url = AddQuery(url, "name", input.Name);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new Exception($"url: {url}, error: invalid absolute URL");

        return new MediaDownloadData { Url = url, Path = path };
    }

    private static string AddQuery(string url, string key, string value)
    {
        string separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
