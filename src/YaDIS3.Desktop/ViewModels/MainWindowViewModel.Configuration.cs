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
    public bool CanExport =>
        !_configuration.BasicMode
        && !string.IsNullOrEmpty(ExportParentPath);

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
            _configurationStore,
            texts: Texts,
            restart: _restart
        );
    }

    private DuneConfiguration CurrentConfiguration() =>
        _configuration with
        {
            Template = SelectedTemplate?.Name ?? "Origins",
            TemplatePath = SelectedTemplate?.Directory ?? "",
            Language = Texts.Current.Code,
            Aai = AaiBackgrounds,
            Cases = UseCases,
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
        await OpenAsync(
            destination,
            cancellationToken: cancellationToken
        );
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
        await OpenAsync(
            WorkspacePath,
            cancellationToken: cancellationToken
        );
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
        MediaContext media = new(card);
        string parent =
            media.Header.Element("path")?.Value ?? "";
        bool included = snapshot.Folders.Any(folder =>
            folder.Path == parent
        );
        bool knownType =
            media.Header.Element("type")?.Value is "0" or "1";
        string? folderFlag = media
            .Header.Element("folder")
            ?.Value;
        return included
            && knownType
            && (
                folderFlag == "yes"
                || (
                    folderFlag == "no"
                    && !_configuration.BasicMode
                )
            );
    }

    private void RememberManualSource(string directory)
    {
        if (_workspace is null)
        {
            return;
        }
        ConfigurationSnapshot snapshot =
            _configurationStore.Load(_workspace.Directory);
        string parent = Path.GetDirectoryName(
            Path.GetFullPath(directory)
        )!;
        if (
            !snapshot.Folders.Any(folder =>
                folder.Path == parent
            )
        )
        {
            _configurationStore.Save(
                _workspace.Directory,
                snapshot with
                {
                    Folders =
                    [
                        .. snapshot.Folders,
                        new(parent, 1, ""),
                    ],
                }
            );
        }
    }
}
