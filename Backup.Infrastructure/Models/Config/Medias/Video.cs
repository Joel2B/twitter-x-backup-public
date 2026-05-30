namespace Backup.Infrastructure.Models.Config.Medias;

public class VideoConfig : MediaConfig
{
    public required MediaConfig Thumb { get; set; }
}
