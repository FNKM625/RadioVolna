using System.Text.Json.Serialization;

namespace RadioVolna;

public class Station
{
    [JsonPropertyName("label")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Url { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName { get; set; } = string.Empty;
}