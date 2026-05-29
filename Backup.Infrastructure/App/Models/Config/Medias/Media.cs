namespace Backup.Infrastructure.Models.Config.Medias;

public class MediaConfig : Filter
{
    public List<string>? Types { get; set; }
    public List<string>? Dimensions { get; set; }
    public List<string>? Sizes { get; set; }
}

