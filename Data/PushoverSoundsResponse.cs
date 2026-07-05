using System.Text.Json.Serialization;

namespace PushoverDesktopClient;

/// <summary>
/// Root structure returned by the auxiliary /1/sounds.json tracking query index endpoint.
/// </summary>
public class PushoverSoundsResponse
{
    /// <summary>
    /// API execution status. Returns 1 if successful.
    /// </summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>
    /// Unique tracking transaction request identifier token.
    /// </summary>
    [JsonPropertyName("request")]
    public string Request { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary containing valid system-wide and custom uploaded alert audio identifiers. 
    /// Keys map directly to parameters used in notification requests (e.g., "bike", "siren"), while values provide friendly descriptions.
    /// </summary>
    [JsonPropertyName("sounds")]
    public Dictionary<string, string> Sounds { get; set; } = [];
}
