namespace Backup.App.Models.Config.Medias;

public class Media : Filter
{
    public List<string>? Types { get; set; }
    public List<string>? Dimensions { get; set; }
    public List<string>? Sizes { get; set; }
}
