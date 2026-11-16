using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EciSupportCentre.Core.Models;

namespace EciSupportCentre.Core.Serialization;

/// <summary>Source-generated JSON metadata (trimming and AOT safe).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(RegisterSearchResult))]
[JsonSerializable(typeof(RegisterStatistics))]
[JsonSerializable(typeof(InitiativeDetails))]
public sealed partial class EciJsonContext : JsonSerializerContext;

/// <summary>Date formats used by the register (Brussels local time).</summary>
internal static class EciDateFormats
{
    public const string Date = "dd/MM/yyyy";
    public const string DateTime = "dd/MM/yyyy HH:mm";

    public static readonly string[] All = [DateTime, Date, "dd/MM/yyyy HH:mm:ss"];

    public static DateTime? Parse(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && System.DateTime.TryParseExact(value.Trim(), All, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;
}

/// <summary>Reads <c>dd/MM/yyyy</c> (or with a time); an empty string becomes null.</summary>
public sealed class EciDateConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && EciDateFormats.Parse(reader.GetString()) is { } dt
            ? DateOnly.FromDateTime(dt)
            : null;

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } v)
        {
            writer.WriteStringValue(v.ToString(EciDateFormats.Date, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>Reads <c>dd/MM/yyyy HH:mm</c> (or a plain date); an empty string becomes null.</summary>
public sealed class EciDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? EciDateFormats.Parse(reader.GetString()) : null;

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } v)
        {
            writer.WriteStringValue(v.ToString(EciDateFormats.DateTime, CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
