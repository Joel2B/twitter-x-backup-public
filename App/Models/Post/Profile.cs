namespace Backup.App.Models.Post;

public class Profile
{
    public required string Id { get; set; }
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
    public Count? Count { get; set; }
}

public class Count
{
    public int? Media { get; set; } = null;
}
