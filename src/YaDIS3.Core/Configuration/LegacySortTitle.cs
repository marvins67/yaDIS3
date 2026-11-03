using System.Globalization;

namespace YaDIS3.Core;

/// <summary>
/// Reprend RemoveArticle sans modifier les titres de tri
/// existants.
/// </summary>
public static class LegacySortTitle
{
    /// <summary>
    /// Retire le premier article correspondant, puis capitalise
    /// la première lettre.
    /// </summary>
    public static string Create(
        string title,
        DuneConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.IgnoreArticles)
        {
            return title;
        }
        foreach (
            string article in configuration.Articles.Split(',')
        )
        {
            string prefix = article.Contains('\'')
                ? article
                : article + " ";
            if (
                title.StartsWith(
                    prefix,
                    StringComparison.CurrentCultureIgnoreCase
                )
                && title.Length > prefix.Length
            )
            {
                string remaining = title[prefix.Length..];
                return char.ToUpper(
                        remaining[0],
                        CultureInfo.CurrentCulture
                    ) + remaining[1..];
            }
        }
        return title;
    }
}
