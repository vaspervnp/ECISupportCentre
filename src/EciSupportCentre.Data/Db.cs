using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EciSupportCentre.Data;

/// <summary>Conversions between .NET values and the text formats stored in SQLite (ISO 8601, sortable).</summary>
internal static class Db
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

    public static Dictionary<string, SqliteParameter> Parameters(SqliteCommand command, params string[] names)
    {
        var parameters = new Dictionary<string, SqliteParameter>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            parameters[name] = command.Parameters.Add(new SqliteParameter { ParameterName = name });
        }

        return parameters;
    }

    public static object From(DateOnly? value) =>
        value?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? (object)DBNull.Value;

    /// <summary>Register times are Brussels local time without an offset; stored as such.</summary>
    public static object From(DateTime? value) =>
        value?.ToString(DateTimeFormat, CultureInfo.InvariantCulture) ?? (object)DBNull.Value;

    /// <summary>Stored in UTC so that text order is time order.</summary>
    public static object From(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) ?? (object)DBNull.Value;

    public static object From(Uri? value) => value?.OriginalString ?? (object)DBNull.Value;

    public static DateOnly? ToDate(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && DateOnly.TryParseExact(reader.GetString(ordinal), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    public static DateTime? ToDateTime(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && DateTime.TryParseExact(reader.GetString(ordinal), DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    public static DateTimeOffset? ToDateTimeOffset(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && DateTimeOffset.TryParse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : null;

    public static Uri? ToUri(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Uri.TryCreate(reader.GetString(ordinal), UriKind.Absolute, out var uri) ? uri : null;
}
