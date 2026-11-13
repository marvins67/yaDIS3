using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Coordonne l'édition des fiches, TVDB, le rendu et l'export.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private DuneTemplate? _selectedTemplate;

    private string _pathMappings = "";

    private string _exportParentPath = "";

    private bool _aaiBackgrounds;

    private bool _useCases;

    private bool _showEpisodePreview;

    private Bitmap? _renderedPreview;

    private CancellationTokenSource? _activeOperation;

    /// <summary>
    /// Templates disponibles pour le rendu et l'export.
    /// </summary>
    public ObservableCollection<DuneTemplate> Templates { get; } =
    [];

    /// <summary>
    /// Commande de traitement de la fiche sélectionnée.
    /// </summary>
    public ICommand ProcessCommand { get; private set; } = null!;

    /// <summary>
    /// Commande d'export du catalogue Dune.
    /// </summary>
    public ICommand ExportCommand { get; private set; } = null!;

    /// <summary>
    /// Commande d'annulation de l'opération en cours.
    /// </summary>
    public ICommand CancelCommand { get; private set; } = null!;

    /// <summary>
    /// Template sélectionné pour le rendu et l'export.
    /// </summary>
    public DuneTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            _selectedTemplate = value;
            Notify();
        }
    }

    /// <summary>
    /// Langue des libellés du template.
    /// </summary>
    public string TemplateLanguage => Texts.Current.Code;

    /// <summary>
    /// Correspondances entre les racines locales et les adresses
    /// Dune.
    /// </summary>
    public string PathMappings
    {
        get => _pathMappings;
        set
        {
            _pathMappings = value;
            Notify();
        }
    }

    /// <summary>
    /// Dossier de destination de l'export géré par
    /// l'application.
    /// </summary>
    public string ExportParentPath
    {
        get => _exportParentPath;
        set
        {
            _exportParentPath = value;
            Notify();
            Notify(nameof(CanExport));
        }
    }

    /// <summary>
    /// Utilise des fonds AAI à la place des JPEG.
    /// </summary>
    public bool AaiBackgrounds
    {
        get => _aaiBackgrounds;
        set
        {
            _aaiBackgrounds = value;
            Notify();
        }
    }

    /// <summary>
    /// Ajoute un boîtier autour des affiches de films.
    /// </summary>
    public bool UseCases
    {
        get => _useCases;
        set
        {
            _useCases = value;
            Notify();
        }
    }

    /// <summary>
    /// Affiche l'aperçu de l'épisode sélectionné.
    /// </summary>
    public bool ShowEpisodePreview
    {
        get => _showEpisodePreview;
        set
        {
            _showEpisodePreview = value;
            RefreshRenderedPreview();
            Notify();
        }
    }

    /// <summary>
    /// Image de l'aperçu actuellement affiché.
    /// </summary>
    public Bitmap? RenderedPreview
    {
        get => _renderedPreview;
        private set
        {
            Replace(ref _renderedPreview, value);
            Notify(nameof(HasRenderedPreview));
        }
    }

    internal CancellationToken OperationToken =>
        _activeOperation?.Token ?? CancellationToken.None;

    private void InitializeDune()
    {
        ExportCommand = new UiCommand(() => RunAsync(ExportDuneAsync));
        ProcessCommand = TreatmentCommand(TreatmentMode.Normal);
        ReloadInformationCommand = TreatmentCommand(
            TreatmentMode.ReloadInformation
        );
        ReloadTechnicalCommand = TreatmentCommand(
            TreatmentMode.ReloadTechnicalInformation
        );
        ProcessAllEpisodesCommand = TreatmentCommand(
            TreatmentMode.AllEpisodes
        );
        CancelCommand = new UiCommand(() =>
        {
            _activeOperation?.Cancel();
            return Task.CompletedTask;
        });
        try
        {
            foreach (
                DuneTemplate template in DuneTemplate.Bundled()
            )
            {
                Templates.Add(template);
            }

            SelectedTemplate = Templates.FirstOrDefault();
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or InvalidDataException
                        or System.Xml.XmlException
            )
        {
            Status = "Templates : " + exception.Message;
        }
    }

    /// <summary>
    /// Charge et sélectionne un template depuis le dossier
    /// choisi.
    /// </summary>
    public void LoadTemplate(string directory)
    {
        DuneTemplate template = DuneTemplate.Load(
            directory,
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                Texts.GetString("341")
            )
        );
        DuneTemplate? existing = Templates.FirstOrDefault(
            templateValue =>
                templateValue.Directory == template.Directory
        );
        if (existing is null)
        {
            Templates.Add(template);
        }

        SelectedTemplate = existing ?? template;
        Status = Texts.GetString("342") + template.Name;
    }

    private void LoadDunePreferences()
    {
        _configuration = _workspace is null
            ? new()
            : _configurationStore
                .Load(_workspace.Directory)
                .Settings;
        _configuration = _configuration with
        {
            Language = Texts.Current.Code,
        };
        PathMappings = _workspace is null
            ? ""
            : string.Join(
                Environment.NewLine,
                _configurationStore
                    .Load(_workspace.Directory)
                    .Folders.Where(folder =>
                        folder.DunePath.Length > 0
                    )
                    .Select(folder =>
                        folder.Path + " => " + folder.DunePath
                    )
            );
        ExportParentPath = _configuration.Destination;
        AaiBackgrounds = _configuration.Aai;
        UseCases = _configuration.Cases;
        SelectedTemplate =
            Templates.FirstOrDefault(value =>
                string.Equals(
                    value.Name,
                    _configuration.Template,
                    StringComparison.OrdinalIgnoreCase
                )
            ) ?? Templates.FirstOrDefault();
        string tvdbSettings = Path.Combine(
            AppContext.BaseDirectory,
            "tvdb-v4.json"
        );
        if (File.Exists(tvdbSettings))
        {
            ConfigureTvdb(tvdbSettings);
        }
        Notify(nameof(CanExport));
    }

    private void SaveDunePreferences()
    {
        if (
            _workspace is not null
            && SelectedTemplate is not null
        )
        {
            _configuration = CurrentConfiguration();
            _configurationStore.SaveSettings(
                _workspace.Directory,
                _configuration
            );
        }
    }

    /// <summary>
    /// Enregistre les fiches et exporte un catalogue Dune
    /// autonome.
    /// </summary>
    public async Task ExportDuneAsync()
    {
        DuneTemplate template =
            SelectedTemplate
            ?? throw new InvalidOperationException(
                "Chargez un template yaDIS3."
            );
        if (string.IsNullOrWhiteSpace(ExportParentPath))
        {
            throw new InvalidOperationException(
                "Choisissez un dossier de destination pour l'export Dune."
            );
        }

        if (!Directory.Exists(ExportParentPath))
        {
            throw new DirectoryNotFoundException(Texts.GetString("125"));
        }

        IReadOnlyList<PathMapping> mappings = DunePaths.Parse(
            PathMappings
        );
        var media = Cards
            .Select(cardValue => new MediaContext(cardValue))
            .ToArray();
        RenderOptions options = ConfiguredRenderOptions();
        string destination = ExportParentPath;
        var progress = new Progress<string>(message =>
            Status = message
        );
        ExportResult result = await Task.Run(
            () =>
                new DuneExporter(
                    template,
                    options,
                    mappings,
                    catalogOptions: ConfiguredCatalogOptions()
                ).ExportLegacy(
                    media,
                    destination,
                    progress,
                    OperationToken
                ),
            OperationToken
        );
        Status =
            Texts.Format(
                "375",
                result.MediaCount,
                result.EpisodeCount,
                result.Directory
            )
            + (
                result.Warnings.Count > 0
                    ? " " + string.Join(" · ", result.Warnings)
                    : ""
            );
    }

    private void RefreshRenderedPreview()
    {
        MediaCard? card = SelectedCard;
        if (card is null)
        {
            RenderedPreview = null;
            return;
        }

        string path = Path.Combine(
            card.DataDirectory,
            "preview.jpg"
        );
        if (
            ShowEpisodePreview
            && SelectedEpisode is { } episode
            && _workspace is not null
        )
        {
            var media = new MediaContext(card);
            XElement? node = media.Episodes.FirstOrDefault(
                element =>
                    (string?)element.Attribute("key")
                    == episode.Key
            );
            if (node is not null)
            {
                string candidate = Path.Combine(
                    LegacyExportPaths.WorkspaceMedia(_workspace.Directory,
                        media.Header.Element("filename")!.Value, media.IsSeries),
                    MediaContext.ViewingSeason(node),
                    MediaContext.EpisodeFolder(node),
                    "preview.jpg"
                );
                if (File.Exists(candidate))
                {
                    path = candidate;
                }
            }
        }

        RenderedPreview = LoadImage(path);
    }
}
