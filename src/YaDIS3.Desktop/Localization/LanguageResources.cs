using Avalonia.Controls;
using YaDIS3.Core;

namespace YaDIS3.Desktop.Localization;

/// <summary>Expose les identifiants XML aux DynamicResource AXAML.</summary>
internal static class LanguageResources
{
    private const int LastApplicationLabel = 377;

    internal static void Apply(
        IResourceDictionary resources,
        LegacyLanguageCatalog catalog
    )
    {
        // Même un identifiant supprimé de tous les XML doit afficher
        // le repli GetString, et non une liaison vide.
        IEnumerable<string> identifiers = Enumerable
            .Range(0, LastApplicationLabel + 1)
            .Select(value =>
                value.ToString(
                    System
                        .Globalization
                        .CultureInfo
                        .InvariantCulture
                )
            )
            .Concat(catalog.Identifiers)
            .Distinct(StringComparer.Ordinal);
        foreach (string identifier in identifiers)
        {
            string text = catalog[identifier];
            string key = "Text." + identifier;
            resources[key] = text;
            resources[key + ".Colon"] = text + ":";
            resources[key + ".SpaceColon"] = text + " :";
            resources[key + ".Ellipsis"] = text + "…";
            resources[key + ".Period"] = text + ".";
            resources[key + ".Percent"] = text + " (%):";
            resources[key + ".App"] = text.Replace(
                "yaDIS",
                "yaDIS3",
                StringComparison.Ordinal
            );
        }
    }
}
