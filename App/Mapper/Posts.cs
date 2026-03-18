using Backup.App.Models.Post;
using Backup.App.Models.Post.Response;

namespace Backup.App.Mapper;

public class Posts : AutoMapper.Profile
{
    public Posts()
    {
        AllowNullCollections = true;

        RegisterMaps();
    }

    public void RegisterMaps()
    {
        CreateMap<Entry, Post>()
            .BeforeMap(
                (src, dest) =>
                {
                    TweetResults? tweetResults = src.Content.ItemContent.TweetResults;

                    if (tweetResults is null)
                        return;

                    Result result = tweetResults.Result;

                    if (result.Tweet is not null)
                        tweetResults.Result = result.Tweet;

                    Result? retweeted = result.Legacy?.RetweetedStatusResult?.Result;

                    if (retweeted is not null)
                    {
                        tweetResults.Result = retweeted;

                        // "__typename": "Tweet" => Core
                        // "__typename": "TweetWithVisibilityResults" => Tweet?.Core

                        if (tweetResults.Result.Tweet is not null)
                            tweetResults.Result = tweetResults.Result.Tweet;
                    }
                }
            )
            .ForMember(
                dest => dest.Id,
                opt =>
                    opt.MapFrom(
                        (src, dest) =>
                        {
                            return src.Content.ItemContent.TweetResults!.Result.Legacy!.IdStr
                                ?? throw new Exception("Id");
                        }
                    )
            )
            .ForMember(
                dest => dest.Profile,
                opt => opt.MapFrom((src, dest) => Profile.GetProfile(src))
            )
            .ForMember(
                dest => dest.Description,
                static opt =>
                    opt.MapFrom<string>(
                        (src, dest) =>
                        {
                            return src.Content.ItemContent.TweetResults!.Result.Legacy!.FullText
                                ?? throw new Exception("Description");
                        }
                    )
            )
            .ForMember(
                dest => dest.Retweeted,
                opt =>
                    opt.MapFrom(src =>
                        src.Content.ItemContent.TweetResults!.Result.Legacy!.Retweeted
                    )
            )
            .ForMember(
                dest => dest.Favorited,
                opt =>
                    opt.MapFrom(src =>
                        src.Content.ItemContent.TweetResults!.Result.Legacy!.Favorited
                    )
            )
            .ForMember(
                dest => dest.Bookmarked,
                opt =>
                    opt.MapFrom(src =>
                        src.Content.ItemContent.TweetResults!.Result.Legacy!.Bookmarked
                    )
            )
            .ForMember(
                dest => dest.CreatedAt,
                opt =>
                    opt.MapFrom(
                        (src, dest) =>
                        {
                            return src.Content.ItemContent.TweetResults!.Result.Legacy!.CreatedAt
                                ?? throw new Exception("CreatedAt");
                        }
                    )
            )
            .ForMember(
                dest => dest.Hashtags,
                opt => opt.MapFrom((src, dest) => Hashtag.GetHashtags(src))
            )
            .ForMember(
                dest => dest.Medias,
                opt => opt.MapFrom((src, dest) => Media.GetMedias(src))
            );
    }
}
