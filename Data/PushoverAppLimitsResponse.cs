using System.Text.Json.Serialization;

namespace PushoverDesktopClient;

/// <summary>
/// Response payload structure returned by the dedicated /apps/limits.json endpoint.
/// Used to track API resource consumption levels.
/// </summary>
public class PushoverAppLimitsResponse
{
    /// <summary>
    /// Total allocation allowance volume limit representing the monthly transmission message cap (e.g., 10000 for users, 25000 for teams).
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>
    /// Remaining transaction allowance balance units available before HTTP 429 throttling policies intercept API calls.
    /// </summary>
    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }

    /// <summary>
    /// Unix timestamp epoch marking when the message metrics counters trigger an automatic allocation reset back to full capacity.
    /// </summary>
    [JsonPropertyName("reset")]
    public long Reset { get; set; }
}
