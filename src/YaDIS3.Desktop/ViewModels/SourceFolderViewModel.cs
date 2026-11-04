using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Copie éditable d'un dossier, validée avant insertion dans la
/// liste.
/// </summary>
public sealed class SourceFolderViewModel(
    MediaSourceFolder folder,
    LegacyDunePathResolver? dunePaths = null
) : ObservableViewModel
{
    private readonly LegacyDunePathResolver _dunePaths =
        dunePaths ?? new(new SystemDunePathEnvironment());

    private string _path = folder.Path;
    private string _dunePath = folder.DunePath;
    private int _type = folder.Type;
    private bool _basicMode;
    private bool _resolving;

    /// <summary>Empêche une seconde sélection pendant la résolution réseau.</summary>
    public bool IsResolving => _resolving;

    /// <summary>
    /// Dossier local ou volume monté.
    /// </summary>
    public string Path
    {
        get => _path;
        set
        {
            _path = value ?? "";
            Notify(null);
        }
    }

    /// <summary>
    /// Le sélecteur écrase le chemin Dune, même s'il avait été
    /// saisi manuellement, comme ButtonFolder_Click de yaDIS2.
    /// </summary>
    public async Task ChooseFolderAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        _resolving = true;
        Notify(null);
        try
        {
            Path = path;
            DunePath = await _dunePaths.ResolveAsync(
                path,
                cancellationToken: cancellationToken
            );
        }
        finally
        {
            _resolving = false;
            Notify(null);
        }
    }

    /// <summary>
    /// Chemin utilisé par le Dune.
    /// </summary>
    public string DunePath
    {
        get => _dunePath;
        set
        {
            _dunePath = value ?? "";
            Notify(null);
        }
    }

    /// <summary>
    /// Films (0) ou séries (1).
    /// </summary>
    public int Type
    {
        get => _type;
        set
        {
            _type = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Type affiché dans la liste des dossiers.
    /// </summary>
    public string TypeLabel => Type == 0 ? "Films" : "Séries TV";

    /// <summary>Exemples proposés dans AddFolderForm.</summary>
    public IReadOnlyList<string> DunePathExamples { get; } =
    [
        "storage_label://label/path",
        "storage_uuid://uuid/path",
        "smb://[user[:password]@]host/share/path",
        "nfs://host[:/export-path]:/path",
    ];

    /// <summary>
    /// Libellés du sélecteur de type.
    /// </summary>
    public IReadOnlyList<string> Types { get; } =
    ["Films", "Séries TV"];

    /// <summary>
    /// Le mode basique ne demande pas de chemin réseau.
    /// </summary>
    public bool BasicMode
    {
        get => _basicMode;
        set
        {
            _basicMode = value;
            Notify(null);
        }
    }

    /// <summary>
    /// Validation d'AddFolderForm reprise du programme original.
    /// </summary>
    public bool CanAccept =>
        !IsResolving
        && Path.Length > 0
        && (BasicMode || DunePath.Length > 0);

    /// <summary>
    /// Conserve les extensions XML du dossier original.
    /// </summary>
    public MediaSourceFolder Snapshot() =>
        folder with
        {
            Path = Path,
            DunePath = DunePath,
            Type = Type,
        };
}
