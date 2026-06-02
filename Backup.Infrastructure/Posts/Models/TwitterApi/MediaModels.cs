using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Models;

public class AllowDownloadStatus
{
    [JsonProperty("allow_download", NullValueHandling = NullValueHandling.Ignore)]
    public bool? AllowDownload { get; set; }
}

public class ExtMediaAvailability
{
    [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    public string? Status { get; set; }
}

public class Face
{
    [JsonProperty("x", NullValueHandling = NullValueHandling.Ignore)]
    public int? X { get; set; }

    [JsonProperty("y", NullValueHandling = NullValueHandling.Ignore)]
    public int? Y { get; set; }

    [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
    public int? H { get; set; }

    [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)]
    public int? W { get; set; }
}

public class Features
{
    [JsonProperty("large", NullValueHandling = NullValueHandling.Ignore)]
    public Large? Large { get; set; }

    [JsonProperty("medium", NullValueHandling = NullValueHandling.Ignore)]
    public Medium? Medium { get; set; }

    [JsonProperty("small", NullValueHandling = NullValueHandling.Ignore)]
    public Small? Small { get; set; }

    [JsonProperty("orig", NullValueHandling = NullValueHandling.Ignore)]
    public Orig? Orig { get; set; }
}

public class FocusRect
{
    [JsonProperty("x", NullValueHandling = NullValueHandling.Ignore)]
    public int? X { get; set; }

    [JsonProperty("y", NullValueHandling = NullValueHandling.Ignore)]
    public int? Y { get; set; }

    [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)]
    public int? W { get; set; }

    [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
    public int? H { get; set; }
}

public class Large
{
    [JsonProperty("faces", NullValueHandling = NullValueHandling.Ignore)]
    public List<Face>? Faces { get; set; }

    [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
    public int? H { get; set; }

    [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)]
    public int? W { get; set; }

    [JsonProperty("resize", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resize { get; set; }
}

public class MediaResults
{
    [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
    public Result? Result { get; set; }
}

public class Medium
{
    [JsonProperty("display_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayUrl { get; set; }

    [JsonProperty("expanded_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? ExpandedUrl { get; set; }

    [JsonProperty("id_str", NullValueHandling = NullValueHandling.Ignore)]
    public required string IdStr { get; set; }

    [JsonProperty("indices", NullValueHandling = NullValueHandling.Ignore)]
    public List<int?>? Indices { get; set; }

    [JsonProperty("media_key", NullValueHandling = NullValueHandling.Ignore)]
    public string? MediaKey { get; set; }

    [JsonProperty("media_url_https", NullValueHandling = NullValueHandling.Ignore)]
    public required string MediaUrlHttps { get; set; }

    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public required string Type { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    [JsonProperty("ext_media_availability", NullValueHandling = NullValueHandling.Ignore)]
    public ExtMediaAvailability? ExtMediaAvailability { get; set; }

    [JsonProperty("features", NullValueHandling = NullValueHandling.Ignore)]
    public Features? Features { get; set; }

    [JsonProperty("sizes", NullValueHandling = NullValueHandling.Ignore)]
    public Sizes? Sizes { get; set; }

    [JsonProperty("original_info", NullValueHandling = NullValueHandling.Ignore)]
    public OriginalInfo? OriginalInfo { get; set; }

    [JsonProperty("video_info", NullValueHandling = NullValueHandling.Ignore)]
    public VideoInfo? VideoInfo { get; set; }

    [JsonProperty("allow_download_status", NullValueHandling = NullValueHandling.Ignore)]
    public AllowDownloadStatus? AllowDownloadStatus { get; set; }

    [JsonProperty("media_results", NullValueHandling = NullValueHandling.Ignore)]
    public MediaResults? MediaResults { get; set; }
}

public class Orig
{
    [JsonProperty("faces", NullValueHandling = NullValueHandling.Ignore)]
    public List<Face>? Faces { get; set; }
}

public class OriginalInfo
{
    [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)]
    public int? Height { get; set; }

    [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)]
    public int? Width { get; set; }

    [JsonProperty("focus_rects", NullValueHandling = NullValueHandling.Ignore)]
    public List<FocusRect>? FocusRects { get; set; }
}

public class Sizes
{
    [JsonProperty("large", NullValueHandling = NullValueHandling.Ignore)]
    public Large? Large { get; set; }

    [JsonProperty("medium", NullValueHandling = NullValueHandling.Ignore)]
    public Medium? Medium { get; set; }

    [JsonProperty("small", NullValueHandling = NullValueHandling.Ignore)]
    public Small? Small { get; set; }

    [JsonProperty("thumb", NullValueHandling = NullValueHandling.Ignore)]
    public Thumb? Thumb { get; set; }
}

public class Small
{
    [JsonProperty("faces", NullValueHandling = NullValueHandling.Ignore)]
    public List<Face>? Faces { get; set; }

    [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
    public int? H { get; set; }

    [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)]
    public int? W { get; set; }

    [JsonProperty("resize", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resize { get; set; }
}

public class Thumb
{
    [JsonProperty("h", NullValueHandling = NullValueHandling.Ignore)]
    public int? H { get; set; }

    [JsonProperty("w", NullValueHandling = NullValueHandling.Ignore)]
    public int? W { get; set; }

    [JsonProperty("resize", NullValueHandling = NullValueHandling.Ignore)]
    public string? Resize { get; set; }
}

public class Variant
{
    [JsonProperty("bitrate", NullValueHandling = NullValueHandling.Ignore)]
    public int? Bitrate { get; set; }

    [JsonProperty("content_type", NullValueHandling = NullValueHandling.Ignore)]
    public required string ContentType { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public required string Url { get; set; }
}

public class VideoInfo
{
    [JsonProperty("aspect_ratio", NullValueHandling = NullValueHandling.Ignore)]
    public required List<int> AspectRatio { get; set; }

    [JsonProperty("duration_millis", NullValueHandling = NullValueHandling.Ignore)]
    public int? DurationMillis { get; set; }

    [JsonProperty("variants", NullValueHandling = NullValueHandling.Ignore)]
    public required List<Variant> Variants { get; set; }
}
