using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Models;

public class EditControl
{
    [JsonProperty("edit_tweet_ids", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? EditTweetIds { get; set; }

    [JsonProperty("editable_until_msecs", NullValueHandling = NullValueHandling.Ignore)]
    public string? EditableUntilMsecs { get; set; }

    [JsonProperty("is_edit_eligible", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEditEligible { get; set; }

    [JsonProperty("edits_remaining", NullValueHandling = NullValueHandling.Ignore)]
    public string? EditsRemaining { get; set; }
}

public class Entities
{
    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public Description? Description { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public UrlDetails? Url { get; set; }

    [JsonProperty("hashtags", NullValueHandling = NullValueHandling.Ignore)]
    public required List<Hashtag> Hashtags { get; set; }

    [JsonProperty("media", NullValueHandling = NullValueHandling.Ignore)]
    public List<Medium>? Media { get; set; }

    [JsonProperty("symbols", NullValueHandling = NullValueHandling.Ignore)]
    public List<object>? Symbols { get; set; }

    [JsonProperty("timestamps", NullValueHandling = NullValueHandling.Ignore)]
    public List<object>? Timestamps { get; set; }

    [JsonProperty("urls", NullValueHandling = NullValueHandling.Ignore)]
    public List<object>? Urls { get; set; }

    [JsonProperty("user_mentions", NullValueHandling = NullValueHandling.Ignore)]
    public List<UserMention>? UserMentions { get; set; }
}

public class ExtendedEntities
{
    [JsonProperty("media", NullValueHandling = NullValueHandling.Ignore)]
    public List<Medium>? Media { get; set; }
}

public class Hashtag
{
    [JsonProperty("indices", NullValueHandling = NullValueHandling.Ignore)]
    public required List<int> Indices { get; set; }

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public required string Text { get; set; }
}

public class Legacy
{
    [JsonProperty("can_dm", NullValueHandling = NullValueHandling.Ignore)]
    public bool? CanDm { get; set; }

    [JsonProperty("can_media_tag", NullValueHandling = NullValueHandling.Ignore)]
    public bool? CanMediaTag { get; set; }

    [JsonProperty("created_at", NullValueHandling = NullValueHandling.Ignore)]
    public string? CreatedAt { get; set; }

    [JsonProperty("default_profile", NullValueHandling = NullValueHandling.Ignore)]
    public bool? DefaultProfile { get; set; }

    [JsonProperty("default_profile_image", NullValueHandling = NullValueHandling.Ignore)]
    public bool? DefaultProfileImage { get; set; }

    [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    [JsonProperty("entities", NullValueHandling = NullValueHandling.Ignore)]
    public required Entities Entities { get; set; }

    [JsonProperty("fast_followers_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? FastFollowersCount { get; set; }

    [JsonProperty("favourites_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? FavouritesCount { get; set; }

    [JsonProperty("followers_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? FollowersCount { get; set; }

    [JsonProperty("friends_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? FriendsCount { get; set; }

    [JsonProperty("has_custom_timelines", NullValueHandling = NullValueHandling.Ignore)]
    public bool? HasCustomTimelines { get; set; }

    [JsonProperty("is_translator", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsTranslator { get; set; }

    [JsonProperty("listed_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? ListedCount { get; set; }

    [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
    public string? Location { get; set; }

    [JsonProperty("media_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? MediaCount { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public required string Name { get; set; }

    [JsonProperty("normal_followers_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? NormalFollowersCount { get; set; }

    [JsonProperty("pinned_tweet_ids_str", NullValueHandling = NullValueHandling.Ignore)]
    public List<string>? PinnedTweetIdsStr { get; set; }

    [JsonProperty("possibly_sensitive", NullValueHandling = NullValueHandling.Ignore)]
    public bool? PossiblySensitive { get; set; }

    [JsonProperty("profile_banner_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? ProfileBannerUrl { get; set; }

    [JsonProperty("profile_image_url_https", NullValueHandling = NullValueHandling.Ignore)]
    public required string ProfileImageUrlHttps { get; set; }

    [JsonProperty("profile_interstitial_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? ProfileInterstitialType { get; set; }

    [JsonProperty("screen_name", NullValueHandling = NullValueHandling.Ignore)]
    public required string ScreenName { get; set; }

    [JsonProperty("statuses_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? StatusesCount { get; set; }

    [JsonProperty("translator_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? TranslatorType { get; set; }

    [JsonProperty("verified", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Verified { get; set; }

    [JsonProperty("want_retweets", NullValueHandling = NullValueHandling.Ignore)]
    public bool? WantRetweets { get; set; }

    [JsonProperty("withheld_in_countries", NullValueHandling = NullValueHandling.Ignore)]
    public List<object>? WithheldInCountries { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    [JsonProperty("following", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Following { get; set; }

    [JsonProperty("notifications", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Notifications { get; set; }

    [JsonProperty("bookmark_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? BookmarkCount { get; set; }

    [JsonProperty("bookmarked", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Bookmarked { get; set; }

    [JsonProperty("conversation_id_str", NullValueHandling = NullValueHandling.Ignore)]
    public string? ConversationIdStr { get; set; }

    [JsonProperty("display_text_range", NullValueHandling = NullValueHandling.Ignore)]
    public List<int?>? DisplayTextRange { get; set; }

    [JsonProperty("extended_entities", NullValueHandling = NullValueHandling.Ignore)]
    public ExtendedEntities? ExtendedEntities { get; set; }

    [JsonProperty("favorite_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? FavoriteCount { get; set; }

    [JsonProperty("favorited", NullValueHandling = NullValueHandling.Ignore)]
    public required bool Favorited { get; set; }

    [JsonProperty("full_text", NullValueHandling = NullValueHandling.Ignore)]
    public string? FullText { get; set; }

    [JsonProperty("is_quote_status", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsQuoteStatus { get; set; }

    [JsonProperty("lang", NullValueHandling = NullValueHandling.Ignore)]
    public string? Lang { get; set; }

    [JsonProperty("possibly_sensitive_editable", NullValueHandling = NullValueHandling.Ignore)]
    public bool? PossiblySensitiveEditable { get; set; }

    [JsonProperty("quote_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? QuoteCount { get; set; }

    [JsonProperty("reply_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? ReplyCount { get; set; }

    [JsonProperty("retweet_count", NullValueHandling = NullValueHandling.Ignore)]
    public int? RetweetCount { get; set; }

    [JsonProperty("retweeted", NullValueHandling = NullValueHandling.Ignore)]
    public required bool Retweeted { get; set; }

    [JsonProperty("user_id_str", NullValueHandling = NullValueHandling.Ignore)]
    public required string UserIdStr { get; set; }

    [JsonProperty("id_str", NullValueHandling = NullValueHandling.Ignore)]
    public string? IdStr { get; set; }

    [JsonProperty("retweeted_status_result", NullValueHandling = NullValueHandling.Ignore)]
    public TweetResults? RetweetedStatusResult { get; set; }

    [JsonProperty("in_reply_to_screen_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? InReplyToScreenName { get; set; }

    [JsonProperty("in_reply_to_status_id_str", NullValueHandling = NullValueHandling.Ignore)]
    public string? InReplyToStatusIdStr { get; set; }

    [JsonProperty("in_reply_to_user_id_str", NullValueHandling = NullValueHandling.Ignore)]
    public string? InReplyToUserIdStr { get; set; }
}

public class Result
{
    [JsonProperty("__typename", NullValueHandling = NullValueHandling.Ignore)]
    public string? Typename { get; set; }

    [JsonProperty("timeline_v2", NullValueHandling = NullValueHandling.Ignore)]
    public TimelineV2? TimelineV2 { get; set; }

    [JsonProperty("rest_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? RestId { get; set; }

    [JsonProperty("core", NullValueHandling = NullValueHandling.Ignore)]
    public CoreUser? Core { get; set; }

    [JsonProperty("tweet", NullValueHandling = NullValueHandling.Ignore)]
    public Result? Tweet { get; set; }

    [JsonProperty("card", NullValueHandling = NullValueHandling.Ignore)]
    public Card? Card { get; set; }

    [JsonProperty("unmention_data", NullValueHandling = NullValueHandling.Ignore)]
    public UnmentionData? UnmentionData { get; set; }

    [JsonProperty("edit_control", NullValueHandling = NullValueHandling.Ignore)]
    public EditControl? EditControl { get; set; }

    [JsonProperty("is_translatable", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsTranslatable { get; set; }

    [JsonProperty("views", NullValueHandling = NullValueHandling.Ignore)]
    public Views? Views { get; set; }

    [JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
    public string? Source { get; set; }

    [JsonProperty("avatar", NullValueHandling = NullValueHandling.Ignore)]
    public Avatar? Avatar { get; set; }

    [JsonProperty("legacy", NullValueHandling = NullValueHandling.Ignore)]
    public Legacy? Legacy { get; set; }

    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public string? Id { get; set; }

    [JsonProperty("affiliates_highlighted_label", NullValueHandling = NullValueHandling.Ignore)]
    public AffiliatesHighlightedLabel? AffiliatesHighlightedLabel { get; set; }

    [JsonProperty("has_graduated_access", NullValueHandling = NullValueHandling.Ignore)]
    public bool? HasGraduatedAccess { get; set; }

    [JsonProperty("is_blue_verified", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsBlueVerified { get; set; }

    [JsonProperty("profile_image_shape", NullValueHandling = NullValueHandling.Ignore)]
    public string? ProfileImageShape { get; set; }

    [JsonProperty("relationship_perspectives", NullValueHandling = NullValueHandling.Ignore)]
    public RelationshipPerspectives? RelationshipPerspectives { get; set; }

    [JsonProperty("tipjar_settings", NullValueHandling = NullValueHandling.Ignore)]
    public TipjarSettings? TipjarSettings { get; set; }

    [JsonProperty("professional", NullValueHandling = NullValueHandling.Ignore)]
    public Professional? Professional { get; set; }

    [JsonProperty("media_key", NullValueHandling = NullValueHandling.Ignore)]
    public string? MediaKey { get; set; }

    [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
    public string? Reason { get; set; }
}

public class UnmentionData { }

public class Views
{
    [JsonProperty("count", NullValueHandling = NullValueHandling.Ignore)]
    public string? Count { get; set; }

    [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
    public string? State { get; set; }
}
