using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Modifie les groupes en conservant le schéma de yaDIS2.</summary>
public static class LegacyMovieGroups
{
    /// <summary>Ajoute ou retire un film de sa saga exclusive.</summary>
    public static void ToggleSaga(
        XDocument groups,
        string movie,
        string name
    )
    {
        XElement target = FindGroup(groups, "saga", name)
            ?? throw new InvalidOperationException("Saga introuvable.");
        XElement? membership = groups.Descendants("saga")
            .Elements("movie")
            .FirstOrDefault(item => item.Value == movie);

        if (membership?.Parent == target)
        {
            RemoveFromSaga(target, membership);
            return;
        }

        if (membership is not null)
        {
            XElement old = membership.Parent!;
            membership.Remove();
            AfterSagaRemoval(old, movie);
        }
        target.Add(new XElement("movie", movie));
    }

    /// <summary>Crée une saga avec son premier film et ses images.</summary>
    public static void CreateSaga(
        XDocument groups,
        string name,
        string movie
    )
    {
        ValidateName(name);
        if (FindGroup(groups, "saga", name) is not null)
        {
            throw new InvalidOperationException("Saga déjà existante.");
        }
        XElement parent = EnsureContainer(groups, "sagas");
        parent.Add(
            new XElement(
                "saga",
                new XElement("name", name),
                new XElement("sort_name", name),
                new XElement("poster", movie),
                new XElement("fanart", movie),
                new XElement("movie", movie)
            )
        );
        XElement? old = groups.Descendants("saga")
            .Where(item => item != parent.Elements("saga").Last())
            .FirstOrDefault(item =>
                item.Elements("movie").Any(entry =>
                    entry.Value == movie
                )
            );
        if (old is not null)
        {
            XElement membership = old.Elements("movie")
                .First(item => item.Value == movie);
            RemoveFromSaga(old, membership);
        }
    }

    /// <summary>Modifie l'appartenance de plusieurs films à une sélection.</summary>
    public static void ToggleSelection(
        XDocument groups,
        string name,
        IReadOnlyList<string> movies
    )
    {
        XElement selection = FindGroup(groups, "selection", name)
            ?? throw new InvalidOperationException(
                "Sélection introuvable."
            );
        bool allSelected = movies.All(movie =>
            selection.Elements("movie").Any(item =>
                item.Value == movie
            )
        );
        foreach (string movie in movies)
        {
            XElement? existing = selection.Elements("movie")
                .FirstOrDefault(item => item.Value == movie);
            if (existing is not null)
            {
                existing.Remove();
            }
            else if (!allSelected)
            {
                selection.Add(new XElement("movie", movie));
            }
        }
        if (!selection.Elements("movie").Any())
        {
            selection.Remove();
        }
    }

    /// <summary>Crée une sélection activée contenant les films choisis.</summary>
    public static void CreateSelection(
        XDocument groups,
        string name,
        IReadOnlyList<string> movies
    )
    {
        ValidateName(name);
        if (FindGroup(groups, "selection", name) is not null)
        {
            throw new InvalidOperationException(
                "Sélection déjà existante."
            );
        }
        XElement selection = new(
            "selection",
            new XAttribute("enabled", "yes"),
            new XElement("name", name),
            movies.Distinct(StringComparer.Ordinal)
                .Select(movie => new XElement("movie", movie))
        );
        EnsureContainer(groups, "selections").Add(selection);
    }

    /// <summary>
    /// Retire un film de ses groupes, y compris les groupes devenus vides.
    /// </summary>
    public static string[] RemoveMovie(XDocument groups, string movie)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentException.ThrowIfNullOrWhiteSpace(movie);
        List<string> removedSagas = [];
        foreach (XElement saga in groups.Descendants("saga").ToArray())
        {
            foreach (XElement member in saga.Elements("movie")
                .Where(item => item.Value == movie).ToArray())
            {
                RemoveFromSaga(saga, member);
            }
            if (saga.Parent is null)
            {
                string name = saga.Element("name")?.Value ?? "";
                if (name.Length > 0)
                {
                    removedSagas.Add(name);
                }
            }
        }
        foreach (XElement selection in groups
            .Descendants("selection").ToArray())
        {
            selection.Elements("movie")
                .Where(item => item.Value == movie).Remove();
            if (!selection.Elements("movie").Any())
            {
                selection.Remove();
            }
        }
        return removedSagas.ToArray();
    }

    /// <summary>Supprime une saga, même si elle contient des films.</summary>
    public static void DeleteSaga(XDocument groups, string name)
    {
        XElement saga = FindGroup(groups, "saga", name)
            ?? throw new InvalidOperationException("Saga introuvable.");
        saga.Remove();
    }

    /// <summary>Supprime une sélection avec ses appartenances.</summary>
    public static void DeleteSelection(XDocument groups, string name)
    {
        XElement selection = FindGroup(groups, "selection", name)
            ?? throw new InvalidOperationException("Sélection introuvable.");
        selection.Remove();
    }

    /// <summary>Renomme une saga et modifie ses références sans perdre les membres.</summary>
    public static void EditSaga(
        XDocument groups,
        string oldName,
        string name,
        string sortName,
        string poster,
        string fanart,
        IReadOnlyList<string> movies
    )
    {
        ValidateName(name);
        XElement saga = FindGroup(groups, "saga", oldName)
            ?? throw new InvalidOperationException("Saga introuvable.");
        if (oldName != name
            && FindGroup(groups, "saga", name) is not null)
        {
            throw new InvalidOperationException("Saga déjà existante.");
        }
        if (movies.Count == 0)
        {
            saga.Remove();
            return;
        }
        if (!movies.Contains(poster) || !movies.Contains(fanart))
        {
            throw new ArgumentException("Illustration hors de la saga.");
        }
        saga.SetElementValue("name", name);
        saga.SetElementValue("sort_name", sortName);
        saga.SetElementValue("poster", poster);
        saga.SetElementValue("fanart", fanart);
        saga.Elements("movie").Remove();
        saga.Add(movies.Select(movie => new XElement("movie", movie)));
    }

    /// <summary>Retourne les membres dans leur ordre historique.</summary>
    public static string[] SagaMembers(XDocument groups, string name) =>
        (FindGroup(groups, "saga", name)
            ?? throw new InvalidOperationException("Saga introuvable."))
        .Elements("movie")
        .Select(movie => movie.Value)
        .ToArray();

    private static XElement EnsureContainer(
        XDocument groups,
        string name
    )
    {
        XElement root = groups.Root
            ?? throw new InvalidDataException("groupes manquants.");
        XElement? existing = root.Element(name);
        if (existing is not null)
        {
            return existing;
        }
        XElement added = new(name);
        root.Add(added);
        return added;
    }

    private static XElement? FindGroup(
        XDocument groups,
        string element,
        string name
    ) =>
        groups.Descendants(element).FirstOrDefault(item =>
            item.Element("name")?.Value == name
        );

    private static void RemoveFromSaga(
        XElement saga,
        XElement movie
    )
    {
        string title = movie.Value;
        movie.Remove();
        AfterSagaRemoval(saga, title);
    }

    private static void AfterSagaRemoval(
        XElement saga,
        string title
    )
    {
        XElement? first = saga.Element("movie");
        if (first is null)
        {
            saga.Remove();
            return;
        }
        foreach (string field in new[] { "poster", "fanart" })
        {
            XElement? reference = saga.Element(field);
            if (reference?.Value == title)
            {
                reference.Value = first.Value;
            }
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(
            ['\\', '/', ':', '*', '?', '"', '<', '>', '|', '&']
        ) >= 0)
        {
            throw new ArgumentException("Caractère interdit.", nameof(name));
        }
    }
}
