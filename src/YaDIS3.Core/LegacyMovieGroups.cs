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
