using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Modifie une copie des préférences ; seule la validation les
/// applique à l'application.
/// </summary>
public sealed partial class ConfigurationViewModel
    : ObservableViewModel,
        IDisposable
{
    private DuneConfiguration _settings;
    private XDocument _groups = new(new XElement("groups"));
    private readonly bool _hasWorkspace;
    private readonly DuneConfigurationStore _configurationStore;
    private readonly Func<
        ConfigurationSnapshot,
        string,
        CancellationToken,
        Task
    > _save;
    private readonly ILogger<ConfigurationViewModel> _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private string _workspace;
    private string _message = "";
    private bool _busy;
    private DuneTemplate? _template;
    private Bitmap? _preview;
    private SourceFolderViewModel? _folder;

    /// <summary>
    /// Injecte les documents et la fonction de validation, sans
    /// écrire à l'ouverture.
    /// </summary>
    public ConfigurationViewModel(
        ConfigurationSnapshot snapshot,
        string workspace,
        IReadOnlyList<DuneTemplate> templates,
        IEnumerable<string> genres,
        Func<
            ConfigurationSnapshot,
            string,
            CancellationToken,
            Task
        > save,
        ILogger<ConfigurationViewModel>? logger = null,
        DuneConfigurationStore? configurationStore = null
    )
    {
        _configurationStore = configurationStore ?? new();
        _logger =
            logger
            ?? NullLogger<ConfigurationViewModel>.Instance;
        _settings = snapshot.Settings;
        _workspace = workspace;
        _hasWorkspace = workspace.Length > 0;
        _save = save;
        Templates = templates;
        LoadDraft(snapshot);
        LoadGenres(genres);
        RemoveFolderCommand = new UiCommand(() =>
        {
            RemoveFolder();
            return Task.CompletedTask;
        });
        AddShortcutCommand = new UiCommand(() =>
        {
            AddShortcut();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Thèmes disponibles dans l'application.
    /// </summary>
    public IReadOnlyList<DuneTemplate> Templates { get; }

    /// <summary>
    /// Langues déjà prises en charge pour le rendu.
    /// </summary>
    public IReadOnlyList<string> Languages { get; } =
    ["fr", "en"];

    /// <summary>
    /// Le périmètre de ce portage est limité à Dune.
    /// </summary>
    public IReadOnlyList<string> Modes { get; } = ["Dune"];

    /// <summary>
    /// Dossiers de la copie de travail.
    /// </summary>
    public ObservableCollection<SourceFolderViewModel> Folders { get; } =
    [];

    /// <summary>
    /// Sélections activables sans modifier leurs membres.
    /// </summary>
    public ObservableCollection<ConfigurationChoice> Selections { get; } =
    [];

    /// <summary>
    /// Catégories 03 à 11 du menu des films.
    /// </summary>
    public ObservableCollection<ConfigurationChoice> Categories { get; } =
    [];

    /// <summary>
    /// Raccourcis ordonnés du menu principal.
    /// </summary>
    public ObservableCollection<DuneShortcut> Shortcuts { get; } =
    [];

    /// <summary>
    /// Genres actuellement présents dans les fiches de films.
    /// </summary>
    public ObservableCollection<string> Genres { get; } = [];

    /// <summary>
    /// Options historiques du tri des nouveautés.
    /// </summary>
    public IReadOnlyList<string> RecentSorts { get; } =
    ["Alphabétique", "Date d'ajout"];

    /// <summary>
    /// Retrait du dossier sélectionné dans la copie de travail.
    /// </summary>
    public ICommand RemoveFolderCommand { get; }

    /// <summary>
    /// Ajout du raccourci proposé.
    /// </summary>
    public ICommand AddShortcutCommand { get; }

    /// <summary>
    /// Chemin proposé, appliqué sur OK.
    /// </summary>
    public string WorkspacePath
    {
        get => _workspace;
        set
        {
            _workspace = value;
            Notify();
        }
    }

    /// <summary>
    /// Erreur de validation affichée dans la fenêtre.
    /// </summary>
    public string Message
    {
        get => _message;
        private set
        {
            _message = value;
            Notify();
        }
    }

    /// <summary>
    /// Empêche la fermeture pendant l'enregistrement.
    /// </summary>
    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            Notify();
        }
    }

    /// <summary>
    /// Options du catalogue central disponibles hors mode
    /// basique.
    /// </summary>
    public bool NormalMode => !BasicMode;

    /// <summary>
    /// Dossier proposé à l'édition ou à la suppression.
    /// </summary>
    public SourceFolderViewModel? SelectedFolder
    {
        get => _folder;
        set
        {
            _folder = value;
            Notify();
            Notify(nameof(HasFolder));
        }
    }

    /// <summary>
    /// Active Éditer et Supprimer.
    /// </summary>
    public bool HasFolder => SelectedFolder is not null;

    /// <summary>
    /// Noms des sélections pour les listes déroulantes.
    /// </summary>
    public IReadOnlyList<string> SelectionNames =>
        Selections.Select(value => value.Name).ToArray();

    /// <summary>
    /// Illustration fournie avec le thème, si présente.
    /// </summary>
    public Bitmap? TemplatePreview => _preview;

    /// <summary>
    /// Auteur et version provenant de template.xml.
    /// </summary>
    public string TemplateDescription =>
        SelectedTemplate is null
            ? ""
            : $"Auteur : {SelectedTemplate.Root.Attribute("author")?.Value}\nVersion : {SelectedTemplate.Root.Attribute("version")?.Value}";

    /// <summary>
    /// Thème proposé sans changer immédiatement le rendu actif.
    /// </summary>
    public DuneTemplate? SelectedTemplate
    {
        get => _template;
        set
        {
            _template = value;
            if (value is not null)
            {
                _settings = _settings with
                {
                    Template = value.Name,
                    TemplatePath = value.Directory,
                };
            }
            LoadPreview(value);
            Notify(null);
        }
    }

    private void LoadPreview(DuneTemplate? template)
    {
        _preview?.Dispose();
        _preview = null;
        string? path = template is null
            ? null
            : Path.Combine(template.Directory, "preview.jpg");
        if (path is null || !File.Exists(path))
        {
            return;
        }
        try
        {
            _preview = new Bitmap(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException or IOException)
        {
            Report("aperçu du thème", exception);
        }
    }

    private void LoadGenres(IEnumerable<string> genres)
    {
        string loaded = "";
        foreach (string genre in genres)
        {
            // Reproduit la recherche littérale de SetupForm, y compris ses sous-chaînes.
            if (
                !loaded.Contains(genre, StringComparison.Ordinal)
            )
            {
                Genres.Add(genre);
                loaded += "|" + genre;
            }
        }
    }

    private void LoadDraft(ConfigurationSnapshot snapshot)
    {
        _settings = snapshot.Settings;
        _groups = new(snapshot.Groups);
        Folders.Clear();
        Selections.Clear();
        Shortcuts.Clear();
        Categories.Clear();
        foreach (MediaSourceFolder folder in snapshot.Folders)
        {
            Folders.Add(new(folder) { BasicMode = BasicMode });
        }
        LoadSelections();
        foreach (DuneShortcut shortcut in snapshot.Shortcuts)
        {
            Shortcuts.Add(shortcut);
        }
        LoadCategories();
        SelectedTemplate =
            Templates.FirstOrDefault(value =>
                value.Directory == _settings.TemplatePath
            )
            ?? Templates.FirstOrDefault(value =>
                value.Name == _settings.Template
            )
            ?? (Templates.Count > 0 ? Templates[0] : null);
        Notify(null);
    }

    private void LoadSelections()
    {
        foreach (
            XElement selection in _groups.Descendants(
                "selection"
            )
        )
        {
            Selections.Add(
                new(
                    selection.Element("name")?.Value ?? "",
                    selection.Attribute("enabled")?.Value
                        == "yes"
                )
            );
        }
    }

    private void LoadCategories()
    {
        string[] names =
        [
            "Alphabet",
            "Genres",
            "Acteurs",
            "Réalisateurs",
            "Durées",
            "Pays",
            "Années",
            "Notes",
            "Formats",
        ];
        foreach (string name in names)
        {
            int index = Categories.Count + 1;
            Categories.Add(
                new(
                    name,
                    index < _settings.Categories.Length
                        && _settings.Categories[index] != '0'
                )
            );
        }
    }

    /// <summary>
    /// Préserve les documents existants si le premier dossier
    /// choisi est déjà un workspace.
    /// </summary>
    public void ChooseWorkspace(string path) =>
        ChoosePath(() =>
        {
            string target = ConfigurationFolderChoice.Resolve(
                path,
                "workspace"
            );
            if (!_hasWorkspace)
            {
                LoadDraft(_configurationStore.Load(target));
            }
            WorkspacePath = target;
        });

    /// <summary>
    /// Applique la proposition du sélecteur à la copie de
    /// travail.
    /// </summary>
    public void ChooseDestination(string path) =>
        ChoosePath(() =>
            Destination = ConfigurationFolderChoice.Resolve(
                path,
                "yaDIS"
            )
        );

    private void ChoosePath(Action choice)
    {
        try
        {
            choice();
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or System.Xml.XmlException
                        or System.Text.Json.JsonException
                        or InvalidDataException
            )
        {
            Report("choix du dossier", exception);
        }
    }

    /// <summary>
    /// Valide l'ajout ou l'édition dans la copie de travail.
    /// </summary>
    public void UpdateFolder(
        SourceFolderViewModel draft,
        SourceFolderViewModel? original = null
    )
    {
        if (!draft.CanAccept)
        {
            throw new ArgumentException(
                "Le dossier et son chemin Dune sont requis.",
                nameof(draft)
            );
        }
        if (original is null)
        {
            Folders.Add(draft);
        }
        else
        {
            Folders[Folders.IndexOf(original)] = draft;
        }
        SelectedFolder = draft;
    }

    private void RemoveFolder()
    {
        if (SelectedFolder is not null)
        {
            Folders.Remove(SelectedFolder);
            SelectedFolder = null;
        }
    }

    /// <summary>
    /// Produit les documents proposés, sans toucher à la
    /// bibliothèque active.
    /// </summary>
    public ConfigurationSnapshot Snapshot() =>
        new(
            _settings with
            {
                Categories =
                    (RecentEnabled ? "1" : "0")
                    + string.Concat(
                        Categories.Select(value =>
                            value.Enabled ? "1" : "0"
                        )
                    )
                    + (
                        _settings.Categories.Length > 10
                            ? _settings.Categories[10..]
                            : ""
                    ),
                Articles = Articles.Replace(
                    " ",
                    "",
                    StringComparison.Ordinal
                ),
            },
            Folders.Select(value => value.Snapshot()).ToArray(),
            SelectedGroups(),
            Shortcuts.ToArray()
        );

    private XDocument SelectedGroups()
    {
        XDocument groups = new(_groups);
        foreach (
            XElement selection in groups.Descendants("selection")
        )
        {
            ConfigurationChoice? choice =
                Selections.FirstOrDefault(value =>
                    value.Name
                    == selection.Element("name")?.Value
                );
            if (choice is not null)
            {
                selection.SetAttributeValue(
                    "enabled",
                    choice.Enabled ? "yes" : "no"
                );
            }
        }
        return groups;
    }

    /// <summary>
    /// Valide puis applique les changements ; un échec laisse le
    /// dialogue ouvert.
    /// </summary>
    public async Task<bool> ApplyAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (IsBusy)
        {
            return false;
        }
        IsBusy = true;
        using CancellationTokenSource operation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetime.Token
            );
        try
        {
            ConfigurationSnapshot snapshot = Snapshot();
            DuneConfigurationStore.Validate(snapshot);
            await _save(
                snapshot,
                WorkspacePath,
                operation.Token
            );
            return true;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or ArgumentException
                        or InvalidOperationException
                        or InvalidDataException
                        or System.Xml.XmlException
                        or System.Text.Json.JsonException
                        or UnauthorizedAccessException
                        or AggregateException
                        or OperationCanceledException
            )
        {
            Report("enregistrement", exception);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Report(string operation, Exception exception)
    {
        ConfigurationFailed(operation, exception);
        Message = exception.Message;
    }

    [LoggerMessage(
        1,
        LogLevel.Error,
        "Échec de configuration Dune pendant {Operation}"
    )]
    private partial void ConfigurationFailed(
        string operation,
        Exception exception
    );

    /// <summary>
    /// Libère l'aperçu et annule l'opération à la fermeture.
    /// </summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _preview?.Dispose();
    }
}
