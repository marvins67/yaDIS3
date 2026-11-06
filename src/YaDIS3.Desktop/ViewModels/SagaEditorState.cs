namespace YaDIS3.Desktop.ViewModels;

/// <summary>Édition en mémoire du contenu d'une saga, avant validation.</summary>
public sealed class SagaEditorState
{
    private readonly List<string> _members;

    /// <summary>Construit un brouillon indépendant du document XML.</summary>
    public SagaEditorState(SagaContextDraft original)
    {
        ArgumentNullException.ThrowIfNull(original);
        Name = original.Name;
        SortName = original.SortName;
        _members = [.. original.Members];
        Poster = original.Poster;
        Fanart = original.Fanart;
    }

    /// <summary>Nom affiché de la saga.</summary>
    public string Name { get; set; }

    /// <summary>Nom utilisé pour le tri.</summary>
    public string SortName { get; set; }

    /// <summary>Membre dont provient la jaquette.</summary>
    public string Poster { get; private set; }

    /// <summary>Membre dont provient le fanart.</summary>
    public string Fanart { get; private set; }

    /// <summary>Ordre courant des membres de la saga.</summary>
    public IReadOnlyList<string> Members => _members;

    /// <summary>Passe à l'image du membre suivant, avec retour au premier.</summary>
    public void NextArtwork(bool poster)
    {
        if (_members.Count == 0)
        {
            return;
        }
        string selected = poster ? Poster : Fanart;
        int next = (_members.IndexOf(selected) + 1) % _members.Count;
        if (poster)
        {
            Poster = _members[next];
        }
        else
        {
            Fanart = _members[next];
        }
    }

    /// <summary>Déplace un membre vers une nouvelle position.</summary>
    public void Move(int source, int destination)
    {
        if (source < 0 || source >= _members.Count
            || destination < 0 || destination >= _members.Count
            || source == destination)
        {
            return;
        }
        string movie = _members[source];
        _members.RemoveAt(source);
        _members.Insert(destination, movie);
    }

    /// <summary>Retire les membres sélectionnés et réaffecte les images si nécessaire.</summary>
    public void Remove(IEnumerable<string> movies)
    {
        HashSet<string> removed = movies.ToHashSet(StringComparer.Ordinal);
        _members.RemoveAll(removed.Contains);
        if (removed.Contains(Poster))
        {
            Poster = _members.FirstOrDefault() ?? "";
        }
        if (removed.Contains(Fanart))
        {
            Fanart = _members.FirstOrDefault() ?? "";
        }
    }

    /// <summary>Prépare les champs à enregistrer uniquement après OK.</summary>
    public SagaContextDraft ToDraft() =>
        new(Name, SortName, Poster, Fanart, [.. _members]);
}
