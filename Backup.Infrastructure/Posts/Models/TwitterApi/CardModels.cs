using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Models;

public class Card
{
    [JsonProperty("legacy", NullValueHandling = NullValueHandling.Ignore)]
    public LegacyCard? LegacyCard { get; set; }
}

public class LegacyCard
{
    [JsonProperty("binding_values", NullValueHandling = NullValueHandling.Ignore)]
    public List<Binding>? BindingValues { get; set; }
}

public class Binding
{
    [JsonProperty("key", NullValueHandling = NullValueHandling.Ignore)]
    public required string Key { get; set; }

    [JsonProperty("value", NullValueHandling = NullValueHandling.Ignore)]
    public required BindingValue Value { get; set; }
}

public class BindingValue
{
    [JsonProperty("string_value", NullValueHandling = NullValueHandling.Ignore)]
    public required string StringValue { get; set; }

    [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    public required string Type { get; set; }
}
