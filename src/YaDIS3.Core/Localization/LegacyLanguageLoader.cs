using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Découverte et sélection reprises de MainModule.LoadLanguages.</summary>
public sealed class LegacyLanguageLoader(IExportFileSystem files)
{
    private readonly IExportFileSystem _files =
        files ?? throw new ArgumentNullException(nameof(files));

    /// <summary>
    /// Une préférence vide suit la langue système et se replie
    /// sur en.xml ; une préférence explicite inconnue est une erreur.
    /// </summary>
    public LegacyLanguageCatalog Load(
        string directory,
        string savedCode,
        CultureInfo culture
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(savedCode);
        ArgumentNullException.ThrowIfNull(culture);
        string code = InitialCode(directory, savedCode, culture);
        List<LanguageDefinition> languages = [];
        HashSet<string> identifiers = new(
            StringComparer.Ordinal
        );
        IReadOnlyDictionary<string, string>? selected = null;
        LanguageDefinition? current = null;
        foreach (
            string file in _files
                .EnumerateFiles(directory)
                .Where(path =>
                    Path.GetExtension(path)
                        .Equals(
                            ".xml",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
        )
        {
            var strings = LegacyLanguageCatalog.ReadStrings(
                XDocument.Parse(_files.ReadText(file))
            );
            LanguageDefinition language = Describe(
                strings,
                file
            );
            languages.Add(language);
            identifiers.UnionWith(strings.Keys);
            if (language.Code == code)
            {
                current = language;
                selected = strings;
            }
        }
        return new(
            current
                ?? throw new InvalidDataException(
                    "Impossible to load the language files !\r\nThe application will close..."
                ),
            languages,
            selected!,
            identifiers
        );
    }

    private string InitialCode(
        string directory,
        string savedCode,
        CultureInfo culture
    )
    {
        if (savedCode.Length > 0)
        {
            return savedCode;
        }
        if (culture.Name.Length < 2)
        {
            throw new InvalidDataException(
                "Impossible to load the language files !\r\nThe application will close..."
            );
        }
        // LoadLanguages prend les deux premiers caractères du nom,
        // pas TwoLetterISOLanguageName (qui peut contenir trois lettres).
        string system = culture.Name[..2];
        return _files.FileExists(
            Path.Combine(directory, system + ".xml")
        )
            ? system
            : "en";
    }

    private static LanguageDefinition Describe(
        IReadOnlyDictionary<string, string> strings,
        string file
    )
    {
        if (
            !strings.TryGetValue("0", out string? code)
            || !strings.TryGetValue("1", out string? name)
        )
        {
            throw new InvalidDataException(
                "Invalid language XML: " + file
            );
        }
        return new(code, name);
    }
}
