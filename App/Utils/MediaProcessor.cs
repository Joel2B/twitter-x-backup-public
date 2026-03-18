using Backup.App.Models.Media;
using Microsoft.AspNetCore.WebUtilities;

namespace Backup.App.Utils;

public class MediaProcessor
{
    public static DataDownload GetData(Models.Media.Processors.Data image)
    {
        List<string?> segments =
        [
            image.PostId,
            image.MediaType,
            image.Id,
            .. image.MidPath,
            image.FormatType,
            image.ResolutionType,
            $"{image.Name}.{image.FormatType}",
        ];

        string path = System.IO.Path.Combine([.. segments.OfType<string>()]);
        string url = image.Url;

        if (image.Options.Query)
        {
            Dictionary<string, string?> query = new()
            {
                { "format", image.FormatType },
                { "name", image.Name },
            };

            url = QueryHelpers.AddQueryString(image.Url, query);
        }

        return new() { Url = url, Path = path };
    }
}
