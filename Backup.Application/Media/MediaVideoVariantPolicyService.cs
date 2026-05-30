using System.Text.RegularExpressions;

namespace Backup.Application.Media;

public sealed class MediaVideoVariantPolicyService : IMediaVideoVariantPolicyService
{
    public string? GetFormatType(string contentType) =>
        contentType switch
        {
            "application/x-mpegURL" => "m3u8",
            "video/mp4" => "mp4",
            _ => null,
        };

    public string? GetResolution(string formatType, string url)
    {
        if (formatType == "m3u8")
            return "master";

        if (formatType == "mp4")
            return Regex.Match(url, @"/(\d+x\d+)/").Groups[1].Value;

        return null;
    }
}
