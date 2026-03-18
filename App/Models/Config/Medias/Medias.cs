namespace Backup.App.Models.Config.Medias;

public class Medias
{
    public bool Enabled { get; set; }
    public required Banner Banner { get; set; }
    public required Profile Profile { get; set; }
    public required Photo Photo { get; set; }
    public required Video Video { get; set; }
    public required Gif Gif { get; set; }
}

public class Banner : Media;

public class Profile : Media;

public class Photo : Media;
