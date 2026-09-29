using System.Text;

namespace CricketLive.Application.Common;

/// <summary>
/// Readable identifiers that still end in the provider's id.
/// </summary>
/// <remarks>
/// <para>
/// Every entity we address by URL is addressed the same way: a kebab-cased prefix a person can
/// read, then the provider id it resolves to. That shape is what lets a slug be looked up without
/// a lookup table, and what lets a stale link keep working after a title is reworded.
/// </para>
/// <para>
/// This lives here, in one place, because a match and a series have to agree on it. They are
/// resolved by different code on different routes, and a second implementation of
/// <see cref="TryExtractId"/> would be a second set of rules about what a valid URL is.
/// </para>
/// </remarks>
public static class Slug
{
    /// <summary>A GUID in the form the provider writes them, which is the only id shape we accept.</summary>
    private const int IdLength = 36;

    /// <summary>
    /// <paramref name="name"/> kebab-cased and suffixed with <paramref name="id"/>, or the bare id
    /// when the name yields nothing usable.
    /// </summary>
    public static string Make(string? name, string id)
    {
        var prefix = Kebab(name);

        return prefix.Length == 0 ? id : $"{prefix}-{id}";
    }

    /// <summary>
    /// The provider id inside <paramref name="idOrSlug"/>, accepting either a bare id or a slug
    /// ending in one.
    /// </summary>
    /// <remarks>
    /// Provider ids are GUIDs, so anything that is not one cannot name an entity and is refused
    /// here. On the match route that is budget protection as well as validation: a request for
    /// <c>/api/matches/wat</c> is answered without spending a call to find out it is nonsense.
    /// </remarks>
    public static bool TryExtractId(string? idOrSlug, out string id)
    {
        var value = idOrSlug?.Trim() ?? string.Empty;

        if (Guid.TryParse(value, out var bare))
        {
            id = bare.ToString();
            return true;
        }

        if (value.Length > IdLength && Guid.TryParse(value[^IdLength..], out var fromSlug))
        {
            id = fromSlug.ToString();
            return true;
        }

        id = string.Empty;
        return false;
    }

    /// <summary>Lower-case, ASCII alphanumerics only, runs of anything else collapsed to one dash.</summary>
    public static string Kebab(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }
}
