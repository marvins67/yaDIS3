using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;
using YaDIS3.Desktop.Localization;
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
    private readonly IApplicationRestart _restart;
    private readonly IExportFileSystem _files;
    private IReadOnlyList<string> _selectionNames = [];

    /// <summary>Langue active, conservée jusqu'au redémarrage.</summary>
    public LegacyLanguageCatalog Texts { get; }

    private readonly LegacyDunePathResolver _dunePaths;
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
        DuneConfigurationStore? configurationStore = null,
        LegacyDunePathResolver? dunePaths = null,
        LegacyLanguageCatalog? texts = null,
        IApplicationRestart? restart = null,
        IExportFileSystem? fileSystem = null
    )
    {
        Texts = texts ?? LegacyLanguageCatalog.Load();
        _files = fileSystem ?? new ExportFileSystem();
        RecentSorts =
        [
            Texts.GetString("165"),
            Texts.GetString("166"),
        ];
        ShortcutTypes =
        [
            Texts.GetString("41"),
            Texts.GetString("172"),
            Texts.GetString("173"),
        ];
        _restart = restart ?? new DesktopApplicationRestart();
        _configurationStore = configurationStore ?? new();
        _logger =
            logger
            ?? NullLogger<ConfigurationViewModel>.Instance;
        _dunePaths =
            dunePaths
            ?? new(
                new SystemDunePathEnvironment(),
                _logger,
                Texts
            );
        _settings = snapshot.Settings with
        {
            Language = Texts.Current.Code,
        };
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
    public IReadOnlyList<LanguageDefinition> Languages =>
        Texts.Languages;

    /// <summary>Choix provisoire soumis à confirmation par la fenêtre.</summary>
    public LanguageDefinition? SelectedLanguage
    {
        get =>
            Languages.FirstOrDefault(item =>
                item.Code == Language
            );
        set
        {
            if (value is not null)
            {
                Language = value.Code;
            }
        }
    }

    /// <summary>Le choix initial ne déclenche pas de demande de redémarrage.</summary>
    public bool HasLanguageChange =>
        Language != Texts.Current.Code;

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
    public IReadOnlyList<string> RecentSorts { get; }

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
        _selectionNames;

    /// <summary>
    /// Illustration fournie avec le thème, si présente.
    /// </summary>
    public Bitmap? TemplatePreview => _preview;

    /// <summary>
    /// Auteur et version provenant de template.xml.
    /// </summary>
    public string TemplateDescription =>
        SelectedTemplate is null || _preview is null
            ? ""
            : Texts["93"]
                + ": "
                + SelectedTemplate
                    .Root.Attribute("author")
                    ?.Value
                + "\r\n"
                + Texts["123"]
                + ": "
                + SelectedTemplate
                    .Root.Attribute("version")
                    ?.Value;

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
        {
            // yaDIS2 n'affiche pas d'erreur pour l'image du thème.
            TemplatePreviewUnavailable(path, exception);
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
        _settings = snapshot.Settings with
        {
            Language = Texts.Current.Code,
        };
        _groups = new(snapshot.Groups);
        Folders.Clear();
        Selections.Clear();
        Shortcuts.Clear();
        Categories.Clear();
        foreach (MediaSourceFolder folder in snapshot.Folders)
        {
            Folders.Add(
                new(folder, texts: Texts)
                {
                    BasicMode = BasicMode,
                }
            );
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
        _selectionNames = Selections
            .Select(value => value.Name)
            .Order(StringComparer.CurrentCulture)
            .ToArray();
    }

    private void LoadCategories()
    {
        string[] names =
        [
            Texts.GetString("43"),
            Texts.GetString("44"),
            Texts.GetString("45"),
            Texts.GetString("46"),
            Texts.GetString("47"),
            Texts.GetString("48"),
            Texts.GetString("49"),
            Texts.GetString("50"),
            Texts.GetString("51"),
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
    public bool ChooseWorkspace(string path) =>
        ChoosePath(
            path,
            "workspace",
            target =>
            {
                if (!_hasWorkspace)
                {
                    LoadDraft(_configurationStore.Load(target));
                }
                WorkspacePath = target;
            }
        );

    /// <summary>
    /// Propose la destination et signale la création du
    /// sous-dossier historique yaDIS.
    /// </summary>
    public bool ChooseDestination(string path) =>
        ChoosePath(
            path,
            "yaDIS",
            target => Destination = target
        );

    private bool ChoosePath(
        string path,
        string suffix,
        Action<string> assign
    )
    {
        try
        {
            string target = ConfigurationFolderChoice.Resolve(
                path,
                suffix,
                _files
            );
            bool created =
                target
                != Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(path)
                );
            if (created)
            {
                _files.CreateDirectory(target);
            }
            assign(target);
            return created;
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
            return false;
        }
    }

    /// <summary>Prépare un ajout ou une édition sans modifier le dossier existant.</summary>
    public SourceFolderViewModel CreateFolderDraft(
        MediaSourceFolder folder
    ) =>
        new(folder, _dunePaths, Texts) { BasicMode = BasicMode };

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

    [LoggerMessage(
        2,
        LogLevel.Warning,
        "Aperçu du thème indisponible : {PreviewPath}"
    )]
    private partial void TemplatePreviewUnavailable(
        string path,
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
