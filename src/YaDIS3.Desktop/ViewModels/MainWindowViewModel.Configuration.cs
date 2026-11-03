using System.Xml.Linq;
using YaDIS3.Core;
using YaDIS3.Rendering;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DuneConfiguration _configuration = new();

    /// <summary>
    /// En mode basique, Traiter écrit directement auprès des
    /// vidéos.
    /// </summary>
    public bool CanExport => !_configuration.BasicMode;

    /// <summary>
    /// Prépare une copie indépendante pour la fenêtre de
    /// configuration.
    /// </summary>
    public ConfigurationViewModel CreateConfiguration()
    {
        ConfigurationSnapshot snapshot = _workspace is null
            ? new(
                _configuration,
                [],
                new XDocument(new XElement("groups")),
                []
            )
            : _configurationStore.Load(_workspace.Directory);
        IEnumerable<string> genres = Cards
            .Where(card => !card.IsSeries)
            .SelectMany(card =>
                new MediaContext(card)
                    .Info.Descendants("genre")
                    .Select(node => node.Value)
            );
        return new(
            snapshot with
            {
                Settings = CurrentConfiguration(),
            },
            WorkspacePath,
            Templates.ToArray(),
            genres,
            ApplyConfigurationAsync,
            _configurationLogger,
            _configurationStore
        );
    }

    private DuneConfiguration CurrentConfiguration() =>
        _configuration with
        {
            Template = SelectedTemplate?.Name ?? "Origins",
            TemplatePath = SelectedTemplate?.Directory ?? "",
            Language = TemplateLanguage,
            Aai = AaiBackgrounds,
            Cases = UseCases,
            Mappings = PathMappings,
            Destination = ExportParentPath,
        };

    private async Task ApplyConfigurationAsync(
        ConfigurationSnapshot snapshot,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        DuneConfigurationStore.Validate(snapshot);
        if (snapshot.Settings.TvdbPath.Length > 0)
        {
            _ = TvdbSettings.Load(snapshot.Settings.TvdbPath);
        }
        string destination = Path.GetFullPath(workspace);
        await SaveAllAsync();
        string? source = _workspace?.Directory;
        await Task.Run(
            () =>
                _configurationService.Apply(
                    source,
                    destination,
                    snapshot,
                    cancellationToken
                ),
            cancellationToken
        );
        await OpenAsync(destination);
        if (snapshot.Settings.TvdbPath.Length > 0)
        {
            ConfigureTvdb(snapshot.Settings.TvdbPath);
        }
        NotifyAll();
    }

    /// <summary>
    /// Redécouvre les dossiers configurés avant de recharger les
    /// fiches.
    /// </summary>
    public async Task RefreshConfiguredWorkspaceAsync(
        CancellationToken cancellationToken = default
    )
    {
        string path = WorkspacePath;
        ConfigurationSnapshot snapshot =
            _configurationStore.Load(path);
        await Task.Run(
            () =>
                _sourceScanner.Scan(
                    path,
                    snapshot.Folders,
                    snapshot.Settings.BasicMode,
                    cancellationToken
                ),
            cancellationToken
        );
        await OpenAsync(path);
    }

    private RenderOptions ConfiguredRenderOptions() =>
        new(
            TemplateLanguage,
            AaiBackgrounds,
            UseCases,
            _configuration.JpegQuality,
            _configuration.BasicMode
        );

    private LegacyCatalogOptions ConfiguredCatalogOptions() =>
        new()
        {
            Categories = _configuration.Categories,
            LastDays = _configuration.LastDays,
            SortRecentByDate = _configuration.SortRecentByDate,
            SelectionBox = _configuration.SelectionBox,
            Scrollbar = _configuration.Scrollbar,
        };

    private bool IsConfiguredSource(
        MediaCard card,
        ConfigurationSnapshot snapshot
    )
    {
        if (!_configuration.SourceFoldersConfigured)
        {
            return true;
        }
        MediaContext media = new(card);
        string parent =
            media.Header.Element("path")?.Value ?? "";
        string source = Path.Combine(
            parent,
            media.Header.Element("filename")?.Value ?? ""
        );
        bool included =
            snapshot.Folders.Any(folder => folder.Path == parent)
            || _configuration.ManualSeriesDirectories.Contains(
                source,
                StringComparer.Ordinal
            );
        return included
            && (
                !_configuration.BasicMode
                || media.Header.Element("folder")?.Value == "yes"
            );
    }

    private void RememberManualSource(string directory)
    {
        _configuration = _configuration with
        {
            ManualSeriesDirectories = _configuration
                .ManualSeriesDirectories.Append(
                    Path.TrimEndingDirectorySeparator(
                        Path.GetFullPath(directory)
                    )
                )
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
        SaveDunePreferences();
    }
}
