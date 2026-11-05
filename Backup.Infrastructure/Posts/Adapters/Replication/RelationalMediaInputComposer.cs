using System.Text.Json;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Adapters;

internal static class RelationalMediaInputComposer
{
    public static List<MediaInput> Compose(
        IEnumerable<PostProfile> profiles,
        IEnumerable<(string PostId, PostMedia Media)> currentMedia,
        IEnumerable<RelationalHistoricalMediaField> history
    )
    {
        List<MediaInput> result = profiles
            .Where(profile =>
                !string.IsNullOrWhiteSpace(profile.ImageUrl)
                || !string.IsNullOrWhiteSpace(profile.BannerUrl)
            )
            .Select(profile => new MediaInput
            {
                Id = $"profile:{profile.Id}",
                Profile = profile.Clone(),
                Medias = null,
            })
            .ToList();

        result.AddRange(
            currentMedia
                .GroupBy(item => item.PostId, StringComparer.Ordinal)
                .Select(group => new MediaInput
                {
                    Id = group.Key,
                    Profile = new PostProfile { Id = string.Empty },
                    Medias = group.Select(item => item.Media.Clone()).ToList(),
                })
        );

        foreach (RelationalHistoricalMediaField field in history)
        {
            if (field.Field == ChangeFields.PostMedias)
            {
                List<PostMedia>? medias = JsonSerializer.Deserialize<List<PostMedia>>(
                    field.OldValueJson
                );

                if (medias is { Count: > 0 })
                {
                    result.Add(
                        new MediaInput
                        {
                            Id = $"history:{field.Sequence}:{field.PostId}",
                            Profile = new PostProfile { Id = string.Empty },
                            Medias = medias,
                        }
                    );
                }

                continue;
            }

            string? oldUrl = JsonSerializer.Deserialize<string?>(field.OldValueJson);

            if (string.IsNullOrWhiteSpace(oldUrl) || string.IsNullOrWhiteSpace(field.ProfileId))
                continue;

            result.Add(
                new MediaInput
                {
                    Id = $"history:{field.Sequence}:{field.PostId}",
                    Profile = new PostProfile
                    {
                        Id = field.ProfileId,
                        ImageUrl = field.Field == ChangeFields.ProfileImageUrl ? oldUrl : null,
                        BannerUrl = field.Field == ChangeFields.ProfileBannerUrl ? oldUrl : null,
                    },
                    Medias = null,
                }
            );
        }

        return result;
    }
}

internal sealed record RelationalHistoricalMediaField(
    int Sequence,
    string PostId,
    string ProfileId,
    string Field,
    string OldValueJson
);
