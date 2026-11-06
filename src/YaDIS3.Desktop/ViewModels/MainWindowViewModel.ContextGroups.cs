using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Commandes de classement accessibles par clic droit.</summary>
public sealed partial class MainWindowViewModel
{
    /// <summary>Demande la saisie d'un nom de groupe à la vue.</summary>
    public event Action<string>? GroupCreationRequested;

    /// <summary>Options disponibles pour la saga du film choisi.</summary>
    public IReadOnlyList<GroupContextChoice> SagaContextChoices =>
        ContextGroupChoices("saga", "186");

    /// <summary>Options disponibles pour les sélections des films choisis.</summary>
    public IReadOnlyList<GroupContextChoice> SelectionContextChoices =>
        ContextGroupChoices("selection", "167");

    private IReadOnlyList<GroupContextChoice> ContextGroupChoices(
        string kind,
        string createLabel
    )
    {
        string[] selected = SelectedContextMovies();
        GroupContextChoice create = new(
            Texts.GetString(createLabel),
            false,
            new UiCommand(() =>
            {
                GroupCreationRequested?.Invoke(kind);
                return Task.CompletedTask;
            })
        );
        string[] names = _filterGroups
            .Descendants(kind)
            .Select(group => group.Element("name")?.Value)
            .OfType<string>()
            .Order(StringComparer.CurrentCulture)
            .ToArray();
        GroupContextChoice[] items = names
            .Select(name => CreateGroupChoice(kind, name, selected))
            .ToArray();
        if (names.Length == 0)
        {
            return [create];
        }

        GroupContextChoice remove = new(
            Texts.GetString("61"),
            false,
            new UiCommand(() => Task.CompletedTask)
        )
        {
            Children = names.Select(name =>
                new GroupContextChoice(
                    name,
                    false,
                    new UiCommand(() =>
                        RunAsync(() => DeleteContextGroupAsync(kind, name))
                    )
                )
            ).ToArray(),
        };
        if (kind != "saga")
        {
            return [create, .. items, remove];
        }

        GroupContextChoice edit = new(
            Texts.GetString("87"),
            false,
            new UiCommand(() => Task.CompletedTask)
        )
        {
            Children = names.Select(name =>
                new GroupContextChoice(
                    name,
                    false,
                    new UiCommand(() =>
                    {
                        SagaEditingRequested?.Invoke(name);
                        return Task.CompletedTask;
                    })
                )
            ).ToArray(),
        };
        return [create, .. items, edit, remove];
    }

    private GroupContextChoice CreateGroupChoice(
        string kind,
        string name,
        string[] selected
    )
    {
        XElement group = _filterGroups.Descendants(kind)
            .Single(item => item.Element("name")?.Value == name);
        int members = selected.Count(movie =>
            group.Elements("movie").Any(entry => entry.Value == movie)
        );
        bool mixed = members > 0 && members < selected.Length;

        // Sans sous-menu, le clic sur le nom lance directement
        // l'ajout, le déplacement ou le retrait comme Saga_MouseDown.
        return new GroupContextChoice(
            mixed ? "◩ " + name : name,
            members > 0 && members == selected.Length,
            new UiCommand(() =>
                RunAsync(() => ChangeContextGroupAsync(kind, name))
            )
        )
        {
            IsIndeterminate = mixed,
            ToggleType = Avalonia.Controls.MenuItemToggleType.CheckBox,
        };
    }

    /// <summary>Signale qu'une saga doit être éditée.</summary>
    public event Action<string>? SagaEditingRequested;

    private string[] SelectedContextMovies() =>
        (
            TreatmentSelection.Count > 0
                ? TreatmentSelection.ToArray()
                : SelectedCard is { } selected
                    ? [selected]
                    : []
        )
        .Where(card => !card.IsSeries)
        .Select(card => card.FolderName)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    /// <summary>Crée un regroupement avec la sélection actuelle.</summary>
    public Task CreateContextGroupAsync(string kind, string name) =>
        RunAsync(async () =>
        {
            string[] movies = SelectedContextMovies();
            if (movies.Length == 0)
            {
                return;
            }

            XDocument groups = new(_filterGroups);
            if (kind == "saga")
            {
                LegacyMovieGroups.CreateSaga(groups, name, movies[0]);
            }
            else if (kind == "selection")
            {
                LegacyMovieGroups.CreateSelection(groups, name, movies);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            await SaveContextGroupsAsync(groups);
        });

    private async Task ChangeContextGroupAsync(
        string kind,
        string name
    )
    {
        string[] movies = SelectedContextMovies();
        if (movies.Length == 0)
        {
            return;
        }
        XDocument groups = new(_filterGroups);
        if (kind == "saga")
        {
            LegacyMovieGroups.ToggleSaga(groups, movies[0], name);
        }
        else
        {
            LegacyMovieGroups.ToggleSelection(groups, name, movies);
        }
        await SaveContextGroupsAsync(groups);
    }

    /// <summary>Donne les paramètres et les membres d'une saga.</summary>
    public SagaContextDraft ReadSaga(string name)
    {
        XElement saga = _filterGroups.Descendants("saga")
            .Single(item => item.Element("name")?.Value == name);
        return new SagaContextDraft(
            name,
            saga.Element("sort_name")?.Value ?? name,
            saga.Element("poster")?.Value ?? "",
            saga.Element("fanart")?.Value ?? "",
            LegacyMovieGroups.SagaMembers(_filterGroups, name)
        );
    }

    /// <summary>Enregistre la modification avancée d'une saga.</summary>
    public Task EditContextSagaAsync(
        string previousName,
        SagaContextDraft draft
    ) =>
        RunAsync(async () =>
        {
            XDocument groups = new(_filterGroups);
            LegacyMovieGroups.EditSaga(
                groups,
                previousName,
                draft.Name,
                draft.SortName,
                draft.Poster,
                draft.Fanart,
                draft.Members
            );
            await SaveContextGroupsAsync(groups);
            CleanupSagaInterface(previousName);
        });

    private async Task DeleteContextGroupAsync(
        string kind,
        string name
    )
    {
        XDocument groups = new(_filterGroups);
        if (kind == "saga")
        {
            LegacyMovieGroups.DeleteSaga(groups, name);
        }
        else
        {
            LegacyMovieGroups.DeleteSelection(groups, name);
        }
        await SaveContextGroupsAsync(groups);
        if (kind == "saga")
        {
            CleanupSagaInterface(name);
        }
    }

    private void CleanupSagaInterface(string name)
    {
        if (_workspace is null)
        {
            return;
        }
        string root = Path.Combine(
            _workspace.Directory, "interface", "00", "98"
        );
        string path = Path.GetFullPath(Path.Combine(root, name));
        if (!path.StartsWith(
            Path.GetFullPath(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase
        ))
        {
            throw new InvalidDataException("Chemin de saga invalide.");
        }
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private async Task SaveContextGroupsAsync(XDocument groups)
    {
        Workspace workspace = _workspace
            ?? throw new InvalidOperationException(
                Texts.GetString("340")
            );
        await Task.Run(
            () => _configurationStore.SaveGroups(
                workspace.Directory,
                groups
            ),
            OperationToken
        );
        string[] removedSagas = _filterGroups
            .Descendants("saga")
            .Select(node => node.Element("name")?.Value ?? "")
            .Except(
                groups.Descendants("saga")
                    .Select(node => node.Element("name")?.Value ?? ""),
                StringComparer.Ordinal
            )
            .ToArray();
        _filterGroups = groups;
        foreach (string name in removedSagas)
        {
            CleanupSagaInterface(name);
        }
        RefreshStateMenu();
        Notify(nameof(SagaContextChoices));
        Notify(nameof(SelectionContextChoices));
    }
}
