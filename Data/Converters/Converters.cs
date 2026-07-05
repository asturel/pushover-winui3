using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PushoverDesktopClient.Converters;

/// <summary>
/// Converts a Unix timestamp in seconds to a non-nullable DateTimeOffset.
/// </summary>
public class UnixSecondsConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
    }
}

/// <summary>
/// Converts a Unix timestamp in seconds to a nullable DateTimeOffset.
/// </summary>
public class NullableUnixSecondsConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value.ToUnixTimeSeconds());
        }
    }
}