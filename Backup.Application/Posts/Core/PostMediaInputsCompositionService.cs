using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostMediaInputsCompositionService : IPostMediaInputsCompositionService
{
    public IReadOnlyList<MediaInput> Compose(IReadOnlyCollection<Post> posts)
    {
        if (posts.Count == 0)
            return [];

        List<MediaInput> current = posts
            .Select(post => new MediaInput
            {
                Id = post.Id,
                Profile = post.Profile.Clone(),
                Medias = post.Medias?.Select(media => media.Clone()).ToList(),
                Deleted = post.Deleted,
            })
            .ToList();

        List<MediaInput> history = posts
            .SelectMany(post => post.Changes)
            .Where(change => change.Data is not null)
            .Select(change => change.Data!)
            .Select(data => new MediaInput
            {
                Id = data.Id,
                Profile = data.Profile.Clone(),
                Medias = data.Medias?.Select(media => media.Clone()).ToList(),
                Deleted = data.Deleted,
            })
            .ToList();

        current.AddRange(history);
        return current;
    }
}
