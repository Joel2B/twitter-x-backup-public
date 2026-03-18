namespace Backup.App.Models.Media.Processors;

public class PostMedia
{
    public required string Id { get; set; }
    public List<Post.Media>? Medias { get; set; }
}
