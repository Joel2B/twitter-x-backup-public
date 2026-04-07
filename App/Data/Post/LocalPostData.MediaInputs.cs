using Backup.App.Models.Data.Json;
using Backup.App.Models.Post;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private sealed class MediaInputState
    {
        public required string PostId { get; set; }
        public required Profile Profile { get; set; }
        public List<Models.Post.Media>? Medias { get; set; }
        public bool Deleted { get; set; }
    }

    private static List<MediaInput> BuildMediaInputs(LocalPostTables tables)
    {
        Dictionary<string, Profile> profiles = tables.Profiles.ToDictionary(
            o => o.Id,
            o => new Profile
            {
                Id = o.Id,
                UserName = o.UserName,
                Name = o.Name,
                BannerUrl = o.BannerUrl,
                ImageUrl = o.ImageUrl,
                Following = o.Following,
                Count = o.CountMedia.HasValue ? new Count { Media = o.CountMedia } : null,
            },
            StringComparer.Ordinal
        );

        Dictionary<(string PostId, int MediaOrdinal), List<Variant>> variantsByMedia = tables
            .MediaVariants.GroupBy(o => (o.PostId, o.MediaOrdinal))
            .ToDictionary(
                g => g.Key,
                g =>
                    g.OrderBy(o => o.Ordinal)
                        .Select(o => new Variant
                        {
                            ContentType = o.ContentType,
                            Bitrate = o.Bitrate,
                            Url = o.Url,
                        })
                        .ToList()
            );

        Dictionary<string, List<Models.Post.Media>> mediasByPost = tables
            .Medias.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.OrderBy(o => o.Ordinal)
                        .Select(o =>
                        {
                            variantsByMedia.TryGetValue((o.PostId, o.Ordinal), out var variants);

                            return new Models.Post.Media
                            {
                                Id = o.MediaId,
                                Url = o.Url,
                                Type = o.Type,
                                VideoInfo =
                                    o.VideoDurationMilis is null
                                    && (variants is null || variants.Count == 0)
                                        ? null
                                        : new VideoInfo
                                        {
                                            DurationMilis = o.VideoDurationMilis,
                                            Variants = variants,
                                        },
                            };
                        })
                        .ToList(),
                StringComparer.Ordinal
            );

        Dictionary<string, MediaInputState> stateByPost = tables.Posts.ToDictionary(
            row => row.Id,
            row =>
            {
                Profile profile = profiles.TryGetValue(row.ProfileId, out Profile? value)
                    ? value.Clone()
                    : new Profile { Id = row.ProfileId };

                mediasByPost.TryGetValue(row.Id, out List<Models.Post.Media>? medias);

                return new MediaInputState
                {
                    PostId = row.Id,
                    Profile = profile,
                    Medias = medias?.Select(media => media.Clone()).ToList(),
                    Deleted = row.Deleted,
                };
            },
            StringComparer.Ordinal
        );

        List<MediaInput> inputs = stateByPost.Values.Select(ToMediaInput).ToList();

        Dictionary<(string PostId, int ChangeOrdinal), List<PostChangeFieldRow>> fieldsByChange =
            tables
                .PostChangeFields.GroupBy(o => (o.PostId, o.ChangeOrdinal))
                .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Ordinal).ToList());

        Dictionary<string, List<PostChangeRow>> changesByPost = tables
            .PostChanges.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(o => o.Date).ThenByDescending(o => o.Ordinal).ToList(),
                StringComparer.Ordinal
            );

        foreach ((string postId, List<PostChangeRow> changes) in changesByPost)
        {
            if (!stateByPost.TryGetValue(postId, out MediaInputState? state))
                continue;

            foreach (PostChangeRow change in changes)
            {
                if (
                    !fieldsByChange.TryGetValue(
                        (change.PostId, change.Ordinal),
                        out List<PostChangeFieldRow>? fields
                    )
                )
                    continue;

                bool changed = false;

                foreach (PostChangeFieldRow field in fields)
                    changed |= ApplyMediaInputOldValue(state, profiles, field);

                if (changed)
                    inputs.Add(ToMediaInput(state));
            }
        }

        return inputs;
    }

    private static bool ApplyMediaInputOldValue(
        MediaInputState state,
        Dictionary<string, Profile> profiles,
        PostChangeFieldRow field
    )
    {
        switch (field.Field)
        {
            case ChangeFields.PostMedias:
                state.Medias = DeserializeJson<List<Models.Post.Media>?>(field.OldValueJson);
                return true;
            case ChangeFields.ProfileImageUrl:
                state.Profile.ImageUrl = DeserializeJson<string?>(field.OldValueJson);
                return true;
            case ChangeFields.ProfileBannerUrl:
                state.Profile.BannerUrl = DeserializeJson<string?>(field.OldValueJson);
                return true;
            case ChangeFields.PostDeleted:
                state.Deleted = DeserializeJson<bool>(field.OldValueJson);
                return true;
            case ChangeFields.ProfileId:
            {
                string? oldProfileId = DeserializeJson<string>(field.OldValueJson);

                if (string.IsNullOrWhiteSpace(oldProfileId))
                    return false;

                if (profiles.TryGetValue(oldProfileId, out Profile? profile))
                    state.Profile = profile.Clone();
                else
                    state.Profile = new Profile { Id = oldProfileId };

                return true;
            }
            default:
                return false;
        }
    }

    private static MediaInput ToMediaInput(MediaInputState state) =>
        new()
        {
            Id = state.PostId,
            Profile = state.Profile.Clone(),
            Medias = state.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = state.Deleted,
        };
}
