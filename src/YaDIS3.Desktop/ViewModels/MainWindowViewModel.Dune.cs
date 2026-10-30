using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Coordinates card editing, TVDB queries, rendering, and
/// export.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private DuneTemplate? _selectedTemplate;

    private string _templateLanguage = "fr";

    private string _pathMappings = "";

    private string _exportParentPath = "";

    private bool _aaiBackgrounds;

    private bool _useCases;

    private bool _showEpisodePreview;

    private Bitmap? _renderedPreview;

    private CancellationTokenSource? _activeOperation;

    /// <summary>
    /// Gets the templates available for rendering and export.
    /// </summary>
    public ObservableCollection<DuneTemplate> Templates { get; } =
    [];

    /// <summary>
    /// Gets the supported template languages.
    /// </summary>
    public IReadOnlyList<string> TemplateLanguages { get; } =
    ["fr", "en"];

    /// <summary>
    /// Gets the command that renders the selected card.
    /// </summary>
    public ICommand ProcessCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command that exports the Dune catalog.
    /// </summary>
    public ICommand ExportCommand { get; private set; } = null!;

    /// <summary>
    /// Gets the command that cancels the active operation.
    /// </summary>
    public ICommand CancelCommand { get; private set; } = null!;

    /// <summary>
    /// Gets or sets the template used for rendering and export.
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
    /// Gets or sets the language used for template labels.
    /// </summary>
    public string TemplateLanguage
    {
        get => _templateLanguage;
        set
        {
            _templateLanguage = value;
            Notify();
        }
    }

    /// <summary>
    /// Gets or sets the local-root to Dune-address mappings.
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
    /// Gets or sets the parent directory for the managed
    /// export.
    /// </summary>
    public string ExportParentPath
    {
        get => _exportParentPath;
        set
        {
            _exportParentPath = value;
            Notify();
        }
    }

    /// <summary>
    /// Gets or sets whether backgrounds use AAI instead of
    /// JPEG.
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
    /// Gets or sets whether film posters include a case frame.
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
    /// Gets or sets whether the selected episode preview is
    /// displayed.
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
    /// Gets the currently displayed rendered preview image.
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

    private CancellationToken OperationToken =>
        _activeOperation?.Token ?? CancellationToken.None;

    private sealed record DunePreferences(
        string Template,
        string TemplatePath,
        string Language,
        bool Aai,
        bool Cases,
        string Mappings,
        string Destination
    );

    private void InitializeDune()
    {
        ProcessCommand = new UiCommand(() =>
            RunAsync(ProcessSelectedAsync)
        );
        ExportCommand = new UiCommand(() =>
            RunAsync(ExportDuneAsync)
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
    /// Loads and selects a template from the chosen directory.
    /// </summary>
    public void LoadTemplate(string directory)
    {
        DuneTemplate template = DuneTemplate.Load(
            directory,
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Images"
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
        Status = "Template chargé : " + template.Name;
    }

    private void LoadDunePreferences()
    {
        PathMappings = "";
        ExportParentPath = "";
        TemplateLanguage = "fr";
        AaiBackgrounds = false;
        UseCases = false;
        SelectedTemplate =
            Templates.FirstOrDefault(templateValue =>
                templateValue.Name == "Origins"
            ) ?? Templates.FirstOrDefault();
        if (_workspace is null)
        {
            return;
        }

        string file = Path.Combine(
            _workspace.Directory,
            "yadis3-modern-export.json"
        );
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            DunePreferences? config =
                JsonSerializer.Deserialize<DunePreferences>(
                    File.ReadAllText(file)
                );
            if (config is null)
            {
                return;
            }

            if (
                File.Exists(
                    Path.Combine(
                        config.TemplatePath,
                        "template.xml"
                    )
                )
            )
            {
                LoadTemplate(config.TemplatePath);
            }
            else
            {
                SelectedTemplate =
                    Templates.FirstOrDefault(templateValue =>
                        templateValue.Name == config.Template
                    ) ?? SelectedTemplate;
            }

            TemplateLanguage = config.Language is "fr" or "en"
                ? config.Language
                : "fr";
            AaiBackgrounds = config.Aai;
            UseCases = config.Cases;
            PathMappings = config.Mappings;
            ExportParentPath = config.Destination;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or JsonException
                        or System.Xml.XmlException
                        or InvalidDataException
            )
        {
            Status =
                "Réglages Dune non chargés : "
                + exception.Message;
        }
    }

    private void SaveDunePreferences()
    {
        if (_workspace is null || SelectedTemplate is null)
        {
            return;
        }

        string file = Path.Combine(
            _workspace.Directory,
            "yadis3-modern-export.json"
        );
        string temporary = file + ".tmp";
        try
        {
            var config = new DunePreferences(
                SelectedTemplate.Name,
                SelectedTemplate.Directory,
                TemplateLanguage,
                AaiBackgrounds,
                UseCases,
                PathMappings,
                ExportParentPath
            );
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    config,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                )
            );
            File.Move(temporary, file, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Saves and renders the selected card using the current
    /// options.
    /// </summary>
    public async Task ProcessSelectedAsync()
    {
        MediaCard card =
            SelectedCard
            ?? throw new InvalidOperationException(
                "Sélectionnez une fiche."
            );
        DuneTemplate template =
            SelectedTemplate
            ?? throw new InvalidOperationException(
                "Chargez un template yaDIS3."
            );
        Workspace current =
            _workspace
            ?? throw new InvalidOperationException(
                "Ouvrez une bibliothèque."
            );
        if (card.IsDirty)
        {
            await _repository.SaveAsync(card, OperationToken);
        }

        SaveDunePreferences();
        var media = new MediaContext(card);
        var options = new RenderOptions(
            TemplateLanguage,
            AaiBackgrounds,
            UseCases
        );
        var progress = new Progress<string>(message =>
            Status = message
        );
        await Task.Run(
            () =>
                DuneExporter.Process(
                    media,
                    template,
                    options,
                    current.Directory,
                    cancellationToken: OperationToken,
                    progress: progress
                ),
            OperationToken
        );
        RefreshImages();
        Status =
            "Rendu terminé : aperçu, icônes et saisons actualisés. Les données vidéo et métadonnées existantes sont conservées.";
    }

    /// <summary>
    /// Saves the cards and exports a standalone Dune catalog.
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

        IReadOnlyList<PathMapping> mappings = DunePaths.Parse(
            PathMappings
        );
        await SaveAllAsync();
        SaveDunePreferences();
        var media = Cards
            .Select(cardValue => new MediaContext(cardValue))
            .ToArray();
        var options = new RenderOptions(
            TemplateLanguage,
            AaiBackgrounds,
            UseCases
        );
        string destination = ExportParentPath;
        var progress = new Progress<string>(message =>
            Status = message
        );
        ExportResult result = await Task.Run(
            () =>
                new DuneExporter(
                    template,
                    options,
                    mappings
                ).Export(
                    media,
                    destination,
                    OperationToken,
                    progress
                ),
            OperationToken
        );
        Status =
            $"Export terminé : {result.MediaCount} fiche(s), {result.EpisodeCount} épisode(s), dans {result.Directory}."
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
                    _workspace.Directory,
                    "interface-yaDIS3",
                    media.Id,
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
