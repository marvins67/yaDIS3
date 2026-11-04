using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Champs du formulaire Film historique.</summary>
public sealed partial class MediaCard
{
    /// <summary>Titre original.</summary>
    public string OriginalTitle
    {
        get => Read("original_title");
        set => Set("original_title", value);
    }

    /// <summary>Titre utilisé pour le classement.</summary>
    public string SortTitle
    {
        get => Read("sort_title");
        set => Set("sort_title", value);
    }

    /// <summary>Durée en minutes.</summary>
    public string Runtime
    {
        get => Read("runtime");
        set => Set("runtime", value);
    }

    /// <summary>Classification du film.</summary>
    public string Certification
    {
        get => Read("certification");
        set => Set("certification", value);
    }

    /// <summary>Accroche du film.</summary>
    public string Tagline
    {
        get => Read("tagline");
        set => Set("tagline", value);
    }

    /// <summary>Acteurs séparés par des virgules.</summary>
    public string Actors
    {
        get => ReadNames("actors", "actor");
        set => SetNames("actors", "actor", value);
    }

    /// <summary>Réalisateurs séparés par des virgules.</summary>
    public string Directors
    {
        get => ReadNames("directors", "director");
        set => SetNames("directors", "director", value);
    }

    /// <summary>Pays séparés par des virgules.</summary>
    public string Countries
    {
        get => ReadNames("countries", "country");
        set => SetNames("countries", "country", value);
    }

    /// <summary>Genres séparés par des virgules.</summary>
    public string Genres
    {
        get => ReadNames("genres", "genre");
        set => SetNames("genres", "genre", value);
    }

    /// <summary>Studios séparés par des virgules.</summary>
    public string Studios
    {
        get => ReadNames("studios", "studio");
        set => SetNames("studios", "studio", value);
    }

    private string ReadNames(string container, string element)
    {
        var names =
            Info?.Descendants(element)
                .Select(node =>
                    node.Element("name")?.Value ?? node.Value
                )
                .ToArray()
            ?? [];
        return names.Length > 0
            ? string.Join(", ", names)
            : Read(container)
                .Replace("|", ", ", StringComparison.Ordinal);
    }

    private void SetNames(
        string container,
        string element,
        string value
    )
    {
        if (ReadNames(container, element) == value)
        {
            return;
        }
        Update(() =>
        {
            XElement info = EnsureInfo();
            XElement group =
                info.Element(container)
                ?? new XElement(container);
            if (group.Parent is null)
            {
                info.Add(group);
            }
            info.Elements(element).Remove();
            // SaveInfos découpe sur chaque virgule et conserve les éléments vides.
            group.ReplaceNodes(
                value
                    .Split(',')
                    .Select(name => new XElement(
                        element,
                        name.Trim()
                    ))
            );
        });
    }
}
