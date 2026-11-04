using System.Text.Json;
using System.Text.Json.Serialization;

namespace IplStore.Api.Serialization;

/// <summary>Timestamps leave the API as UTC with a trailing Z. The client decides how to display them (IST by default).</summary>
public sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTimeOffset().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.UtcDateTime);
    }
}
