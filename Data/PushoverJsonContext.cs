using System.Text.Json.Serialization;

namespace PushoverDesktopClient;

/// <summary>
/// Compile-time source generated serialization context for performance optimization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PushoverMessageResponse))]
[JsonSerializable(typeof(PushoverMessageItem))]
[JsonSerializable(typeof(PushoverAppLimitsResponse))]
[JsonSerializable(typeof(PushoverSoundsResponse))]
public partial class PushoverJsonContext : JsonSerializerContext
{
}