using System.Text;
using System.Text.Json;
using IplStore.Application.Common;
using IplStore.Domain.Exceptions;

namespace IplStore.Application.Paging;

/// <summary>
/// Opaque keyset pagination tokens (URL safe base64 of a <see cref="KeysetCursor"/>).
/// A token only says "continue after this sort key and id". It grants no access by itself.
/// </summary>
public static class PageToken
{
    public static string Encode(KeysetCursor cursor)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static KeysetCursor? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            var padding = (4 - base64.Length % 4) % 4;
            base64 = base64.PadRight(base64.Length + padding, '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var cursor = JsonSerializer.Deserialize<KeysetCursor>(json);
            if (cursor is null || string.IsNullOrEmpty(cursor.SortKey) || string.IsNullOrEmpty(cursor.Id))
            {
                throw new FormatException();
            }

            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new ValidationException("pageToken", "The page token is invalid.");
        }
    }

    /// <summary>The token for the page after <paramref name="slice"/>, or null when it is the last page.</summary>
    public static string? ForNextPage<T>(Slice<T> slice, Func<T, KeysetCursor> cursorOf)
    {
        if (!slice.HasMore || slice.Items.Count == 0)
        {
            return null;
        }

        return Encode(cursorOf(slice.Items[^1]));
    }

    /// <summary>Applies the default page size and caps it. Page size is never unbounded.</summary>
    public static int NormalizeSize(int? requested)
    {
        if (requested is null)
        {
            return StoreOptions.DefaultPageSize;
        }

        if (requested < 1)
        {
            throw new ValidationException("pageSize", "Page size must be at least 1.");
        }

        return Math.Min(requested.Value, StoreOptions.MaxPageSize);
    }
}
