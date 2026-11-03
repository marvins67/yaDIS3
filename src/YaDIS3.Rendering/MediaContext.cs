using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Fige les métadonnées utilisées pendant un rendu ou un export.
/// </summary>
public sealed class MediaContext
{
    /// <summary>
    /// Racine XML des données chargées.
    /// </summary>
    public XElement Root { get; }

    /// <summary>
    /// En-tête historique de la fiche.
    /// </summary>
    public XElement Header { get; }

    /// <summary>
    /// Métadonnées utilisées pour dessiner la fiche.
    /// </summary>
    public XElement Info { get; }

    /// <summary>
    /// Dossier contenant les métadonnées et les illustrations de
    /// la fiche.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Titre affiché.
    /// </summary>
    public string Title =>
        Info.ValueAt("title") is { Length: > 0 } title
            ? title
            : Header.ValueAt("filename");

    /// <summary>
    /// Indique si la fiche représente une série.
    /// </summary>
    public bool IsSeries => Header.ValueAt("type") == "1";

    /// <summary>
    /// Identifiant stable de cette entrée du catalogue.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Épisodes classés dans leur ordre de navigation
    /// enregistré.
    /// </summary>
    public IReadOnlyList<XElement> Episodes { get; }

    /// <summary>
    /// Copie les métadonnées afin de stabiliser le rendu et
    /// l'export.
    /// </summary>
    public MediaContext(MediaCard card)
    {
        Root = card.Snapshot().Document.Root!;
        Header =
            Root.Name == "item" ? Root : Root.Element("item")!;
        Info =
            Root.Element("info")
            ?? Header.Element("info")
            ?? new XElement(
                "info",
                new XElement("title", Header.ValueAt("filename"))
            );
        DataDirectory = card.DataDirectory;
        string seed =
            (IsSeries ? "tv:" : "movie:")
            + Path.GetFileName(DataDirectory)
                .Normalize(NormalizationForm.FormC);
        Id = Convert
            .ToHexString(
                SHA256
                    .HashData(Encoding.UTF8.GetBytes(seed))
                    .AsSpan(0, 10)
            )
            .ToLowerInvariant();
        Episodes = Info.Elements("episode")
            .OrderBy(
                element => element.Attr("key"),
                StringComparer.Ordinal
            )
            .ToArray();
    }

    /// <summary>
    /// Résout le chemin d'une illustration dans le dossier de la
    /// fiche.
    /// </summary>
    public string Artwork(string name)
    {
        return Path.Combine(DataDirectory, name);
    }

    /// <summary>
    /// Numéro de saison enregistré pour l'épisode.
    /// </summary>
    public static int Season(XElement episode)
    {
        return episode.Int("season");
    }

    /// <summary>
    /// Numéro de l'épisode dans sa saison.
    /// </summary>
    public static int Number(XElement episode)
    {
        return episode.Int("episode");
    }

    // La clé historique peut ranger les épisodes spéciaux dans une autre saison.

    /// <summary>
    /// Saison de navigation conservant le placement des épisodes
    /// spéciaux.
    /// </summary>
    public static string ViewingSeason(XElement episode)
    {
        return LegacyExportPaths.Season(EpisodeFolder(episode));
    }

    /// <summary>
    /// Nom de dossier stable et compatible avec la navigation
    /// Dune.
    /// </summary>
    public static string EpisodeFolder(XElement episode)
    {
        string key = episode.Attr("key");
        return System.Text.RegularExpressions.Regex.IsMatch(
            key,
            "^[0-9]{2,3}_[0-9]{3,5}_[0-9]{3,5}$",
            System
                .Text
                .RegularExpressions
                .RegexOptions
                .CultureInvariant
        )
            ? key
            : $"{Season(episode):00}_{Number(episode):000}_000";
    }
}
