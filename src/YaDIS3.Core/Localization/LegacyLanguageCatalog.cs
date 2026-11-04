using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Équivalent de XmlStrings/GetString : aucune substitution
/// française pour un identifiant absent du catalogue sélectionné.
/// </summary>
public sealed class LegacyLanguageCatalog
{
    private readonly IReadOnlyDictionary<
        string,
        string
    > _strings;

    /// <summary>Langue sélectionnée dans les XML.</summary>
    public LanguageDefinition Current { get; }

    /// <summary>Langues dans l'ordre de découverte des fichiers.</summary>
    public IReadOnlyList<LanguageDefinition> Languages { get; }

    /// <summary>Identifiants connus pour les liaisons de l'interface.</summary>
    public IReadOnlyCollection<string> Identifiers { get; }

    internal LegacyLanguageCatalog(
        LanguageDefinition current,
        IReadOnlyList<LanguageDefinition> languages,
        IReadOnlyDictionary<string, string> strings,
        IReadOnlyCollection<string> identifiers
    )
    {
        Current = current;
        Languages = languages;
        _strings = strings;
        Identifiers = identifiers;
    }

    /// <summary>Recherche le même identifiant que GetString.</summary>
    public string this[string identifier] =>
        GetString(identifier);

    /// <summary>Conserve le repli historique, y compris sa casse.</summary>
    public string GetString(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return _strings.TryGetValue(
            identifier,
            out string? value
        )
            ? value
            : "<label not found>";
    }

    /// <summary>Formate les messages ajoutés par yaDIS3.</summary>
    public string Format(
        string identifier,
        params object?[] arguments
    ) =>
        string.Format(
            CultureInfo.CurrentCulture,
            GetString(identifier),
            arguments
        );

    /// <summary>Catalogue distribué auprès de l'exécutable.</summary>
    public static LegacyLanguageCatalog Load(
        string code = "fr"
    ) =>
        new LegacyLanguageLoader(new ExportFileSystem()).Load(
            Path.Combine(AppContext.BaseDirectory, "languages"),
            code,
            CultureInfo.CurrentUICulture
        );

    internal static IReadOnlyDictionary<
        string,
        string
    > ReadStrings(XDocument document) =>
        document
            .Descendants()
            .Where(node => node.Attribute("id") is not null)
            .GroupBy(
                node => (string)node.Attribute("id")!,
                StringComparer.Ordinal
            )
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.Ordinal
            );
}
