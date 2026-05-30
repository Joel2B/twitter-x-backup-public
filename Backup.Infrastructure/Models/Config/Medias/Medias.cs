namespace Backup.Infrastructure.Models.Config.Medias;

public class MediasConfig
{
    public bool Enabled { get; set; }
    public required BannerConfig Banner { get; set; }
    public required ProfileConfig Profile { get; set; }
    public required PhotoConfig Photo { get; set; }
    public required VideoConfig Video { get; set; }
    public required GifConfig Gif { get; set; }
}

public class BannerConfig : MediaConfig;

public class ProfileConfig : MediaConfig;

public class PhotoConfig : MediaConfig;
