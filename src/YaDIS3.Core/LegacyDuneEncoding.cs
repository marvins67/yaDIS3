using System.Text;

namespace YaDIS3.Core;

/// <summary>
/// Reproduit le contrat texte de MainModule.CreateDF dans
/// yaDIS2.
/// </summary>
public static class LegacyDuneEncoding
{
    /// <summary>
    /// Identifie les fichiers de navigation gérés par yaDIS.
    /// </summary>
    public const string Header =
        "*** NE PAS SUPPRIMER OU MODIFIER CETTE LIGNE - DO NOT DELETE OR MODIFY THIS LINE ***";

    /// <summary>
    /// Produit le même UTF-8 avec BOM et les mêmes fins de ligne
    /// Windows.
    /// </summary>
    public static byte[] Encode(
        string content,
        bool aaiBackgrounds
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!aaiBackgrounds)
        {
            content = content
                .Replace(
                    "background.aai",
                    "background.jpg",
                    StringComparison.Ordinal
                )
                .Replace(
                    "background2.aai",
                    "background2.jpg",
                    StringComparison.Ordinal
                );
        }

        string text =
            Header
            + "\r\n"
            + string.Join("\r\n", content.Split('|'))
            + "\r\n";
        UTF8Encoding encoding = new(true);
        return
        [
            .. encoding.GetPreamble(),
            .. encoding.GetBytes(text),
        ];
    }
}
