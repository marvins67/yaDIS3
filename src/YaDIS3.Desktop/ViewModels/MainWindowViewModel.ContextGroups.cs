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
        GroupContextChoice[] items = _filterGroups
            .Descendants(kind)
            .Where(group => group.Element("name") is not null)
            .OrderBy(
                group => group.Element("name")!.Value,
                StringComparer.CurrentCulture
            )
            .Select(group =>
            {
                string name = group.Element("name")!.Value;
                bool checkedItem = selected.Length > 0
                    && selected.All(movie =>
                        group.Elements("movie").Any(entry =>
                            entry.Value == movie
                        )
                    );
                return new GroupContextChoice(
                    name,
                    checkedItem,
                    new UiCommand(() =>
                        RunAsync(() =>
                            ChangeContextGroupAsync(kind, name)
                        )
                    )
                );
            })
            .ToArray();

        return [create, .. items];
    }

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
        _filterGroups = groups;
        RefreshStateMenu();
        Notify(nameof(SagaContextChoices));
        Notify(nameof(SelectionContextChoices));
    }
}
