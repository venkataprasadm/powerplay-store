using System.Globalization;

namespace IplStore.Domain.Common;

/// <summary>
/// Timestamps are stored as fixed-width UTC strings so that string order equals time order.
/// That is what makes keyset pagination and ORDER BY in Cosmos DB correct.
/// </summary>
public static class Timestamp
{
    private const string StorageFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private const DateTimeStyles UtcStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public static string Format(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString(StorageFormat, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.ParseExact(value, StorageFormat, CultureInfo.InvariantCulture, UtcStyles);
    }

    public static bool TryParse(string? value, out DateTimeOffset result)
    {
        result = default;
        if (value is null)
        {
            return false;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, UtcStyles, out result);
    }
}
