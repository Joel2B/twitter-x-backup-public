namespace Backup.Infrastructure.Posts.Models;

public class PostProfile
{
    public required string Id { get; set; }
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
    public PostCount? Count { get; set; }

    public PostProfile Clone() =>
        new()
        {
            Id = Id,
            UserName = UserName,
            Name = Name,
            BannerUrl = BannerUrl,
            ImageUrl = ImageUrl,
            Following = Following,
            Count = Count?.Clone(),
        };
}

public class PostCount
{
    public int? Media { get; set; } = null;

    public PostCount Clone() => new() { Media = Media };
}
