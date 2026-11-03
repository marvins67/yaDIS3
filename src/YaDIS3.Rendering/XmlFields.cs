using System.Globalization;
using System.Xml.Linq;

namespace YaDIS3.Rendering;

internal static class XmlFields
{
    /// <summary>
    /// Recherche un élément XML par un chemin relatif séparé par
    /// des barres obliques.
    /// </summary>
    public static XElement? XPath(
        this XElement node,
        string path
    )
    {
        XElement? current = node;
        foreach (
            string part in path.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            current = current?.Element(part);
        }

        return current;
    }

    /// <summary>
    /// Lit le texte d'un élément XML ; renvoie une chaîne vide
    /// en son absence.
    /// </summary>
    public static string ValueAt(this XElement node, string path)
    {
        return node.XPath(path)?.Value ?? "";
    }

    /// <summary>
    /// Lit un attribut XML ou sa valeur de remplacement.
    /// </summary>
    public static string Attr(
        this XElement? node,
        string name,
        string fallback = ""
    )
    {
        return (string?)node?.Attribute(name) ?? fallback;
    }

    /// <summary>
    /// Lit un attribut XML entier ou sa valeur de remplacement.
    /// </summary>
    public static int Int(
        this XElement? node,
        string name,
        int fallback = 0
    )
    {
        return int.TryParse(
            node.Attr(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : fallback;
    }

    /// <summary>
    /// Lit un attribut XML numérique fini ou sa valeur de
    /// remplacement.
    /// </summary>
    public static float Float(
        this XElement? node,
        string name,
        float fallback = 0
    )
    {
        return
            float.TryParse(
                node.Attr(name),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number
            ) && float.IsFinite(number)
            ? number
            : fallback;
    }
}
