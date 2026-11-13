using System.ComponentModel;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Édite un épisode à l'intérieur de sa fiche de série.
/// </summary>
public sealed class Episode(MediaCard owner, XElement node)
    : INotifyPropertyChanged
{
    internal XElement Node => node;

    /// <summary>
    /// Signale aux liaisons de données que l'état affiché a
    /// changé.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private static int Numeric(
        XElement element,
        string attribute
    )
    {
        return
            int.TryParse(
                (string?)element.Attribute(attribute),
                out var number
            )
            && number >= 0
            ? number
            : 0;
    }

    /// <summary>
    /// Numéro de saison enregistré pour l'épisode.
    /// </summary>
    public int Season => Numeric(node, "season");

    /// <summary>
    /// Numéro de l'épisode dans sa saison.
    /// </summary>
    public int Number => Numeric(node, "episode");

    /// <summary>
    /// Clé de navigation de l'épisode.
    /// </summary>
    public string Key =>
        (string?)node.Attribute("key")
        ?? $"{Season:00}_{Number:000}_000";

    /// <summary>
    /// Libellé affiché dans la liste de sélection.
    /// </summary>
    public string Label =>
        $"S{Season:00} · E{Number:00} — {Title}";

    /// <summary>
    /// Indique si les métadonnées ont été modifiées
    /// manuellement.
    /// </summary>
    public bool IsManual =>
        (string?)node.Attribute("manual_edit") == "yes";

    private string Get(string field)
    {
        return node.Element(field)?.Value ?? "";
    }

    private void Set(string field, string? value)
    {
        if (Get(field) == (value ?? ""))
        {
            return;
        }

        owner.UpdateEpisode(() =>
        {
            MediaCard.Put(node, field, value);
            node.SetAttributeValue("manual_edit", "yes");
        });
    }

    /// <summary>
    /// Titre affiché.
    /// </summary>
    public string Title
    {
        get => Get("title");
        set => Set("title", value);
    }

    /// <summary>
    /// Synopsis.
    /// </summary>
    public string Plot
    {
        get => Get("plot");
        set => Set("plot", value);
    }

    /// <summary>
    /// Date de première diffusion de l'épisode.
    /// </summary>
    public string Aired
    {
        get => Get("aired");
        set => Set("aired", value);
    }

    /// <summary>
    /// Note conservée sous sa forme textuelle.
    /// </summary>
    public string Rating
    {
        get => Get("rating");
        set => Set("rating", value);
    }

    /// <summary>
    /// Noms des réalisateurs de l'épisode.
    /// </summary>
    public string Director
    {
        get => Get("director");
        set => Set("director", value);
    }

    /// <summary>
    /// Chemin du fichier vidéo local de l'épisode.
    /// </summary>
    public string VideoPath => Get("file");

    /// <summary>
    /// Chemin de la miniature d'épisode, importée ou
    /// enregistrée.
    /// </summary>
    public string ImagePath =>
        owner.ArtworkPath($"{Season}x{Number}.jpg");

    internal void MarkImageManual()
    {
        MediaCard.Put(node, "image", "");
        node.SetAttributeValue(
            "manual_image",
            $"{Season}x{Number}.jpg"
        );
    }

    internal void Notify()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty)
        );
    }
}
