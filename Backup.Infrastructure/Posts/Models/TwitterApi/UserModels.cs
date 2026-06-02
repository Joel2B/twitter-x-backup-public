using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Models;

public class AffiliatesHighlightedLabel { }

public class Avatar
{
    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public required string ImageUrl { get; set; }
}

public class Category
{
    [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    public int? Id { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("icon_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? IconName { get; set; }
}

public class CoreUser
{
    [JsonProperty("user_results", NullValueHandling = NullValueHandling.Ignore)]
    public required UserResults UserResults { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public required string Name { get; set; }

    [JsonProperty("screen_name", NullValueHandling = NullValueHandling.Ignore)]
    public required string ScreenName { get; set; }
}

public class DataUser
{
    [JsonProperty("user", NullValueHandling = NullValueHandling.Ignore)]
    public required User User { get; set; }
}

public class Description
{
    [JsonProperty("urls", NullValueHandling = NullValueHandling.Ignore)]
    public List<UrlDetails>? Urls { get; set; }
}

public class Professional
{
    [JsonProperty("rest_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? RestId { get; set; }

    [JsonProperty("professional_type", NullValueHandling = NullValueHandling.Ignore)]
    public string? ProfessionalType { get; set; }

    [JsonProperty("category", NullValueHandling = NullValueHandling.Ignore)]
    public List<Category>? Category { get; set; }
}

public class RelationshipPerspectives
{
    [JsonProperty("following", NullValueHandling = NullValueHandling.Ignore)]
    public required bool Following { get; set; }
}

public class TipjarSettings
{
    [JsonProperty("is_enabled", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsEnabled { get; set; }

    [JsonProperty("bitcoin_handle", NullValueHandling = NullValueHandling.Ignore)]
    public string? BitcoinHandle { get; set; }
}

public class UrlDetails
{
    [JsonProperty("display_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? DisplayUrl { get; set; }

    [JsonProperty("expanded_url", NullValueHandling = NullValueHandling.Ignore)]
    public string? ExpandedUrl { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }

    [JsonProperty("indices", NullValueHandling = NullValueHandling.Ignore)]
    public List<int?>? Indices { get; set; }
}

public class User
{
    [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
    public required Result Result { get; set; }
}

public class UserMention
{
    [JsonProperty("id_str", NullValueHandling = NullValueHandling.Ignore)]
    public string? IdStr { get; set; }

    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name { get; set; }

    [JsonProperty("screen_name", NullValueHandling = NullValueHandling.Ignore)]
    public string? ScreenName { get; set; }

    [JsonProperty("indices", NullValueHandling = NullValueHandling.Ignore)]
    public List<int?>? Indices { get; set; }
}

public class UserResults
{
    [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
    public Result? Result { get; set; }
}
