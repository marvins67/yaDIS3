using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Copie éditable d'un dossier, validée avant insertion dans la
/// liste.
/// </summary>
public sealed class SourceFolderViewModel : ObservableViewModel
{
    private readonly LegacyDunePathResolver _dunePaths;
    private readonly MediaSourceFolder _folder;

    /// <summary>Libellés du catalogue actif.</summary>
    public LegacyLanguageCatalog Texts { get; }

    private string _path;
    private string _dunePath;
    private int _type;
    private bool _basicMode;
    private bool _resolving;
    private bool _dunePathEdited;

    /// <summary>Prépare les listes une seule fois pour conserver la sélection.</summary>
    public SourceFolderViewModel(
        MediaSourceFolder folder,
        LegacyDunePathResolver? dunePaths = null,
        LegacyLanguageCatalog? texts = null
    )
    {
        ArgumentNullException.ThrowIfNull(folder);
        _folder = folder;
        Texts = texts ?? LegacyLanguageCatalog.Load();
        _dunePaths =
            dunePaths
            ?? new(
                new SystemDunePathEnvironment(),
                texts: Texts
            );
        _path = folder.Path;
        _dunePath = folder.DunePath;
        _dunePathEdited = _dunePath.Length > 0;
        _type = folder.Type;
        Types = [Texts["63"], Texts["64"]];
    }

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
    /// Le sélecteur propose un chemin Dune tant que celui-ci n'a pas été saisi
    /// ou enregistré. Un chemin configuré doit rester sous contrôle utilisateur.
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
            if (!_dunePathEdited)
            {
                string proposed = await _dunePaths.ResolveAsync(
                    path,
                    cancellationToken: cancellationToken
                );
                // Une saisie effectuée pendant la résolution DNS prime aussi
                // sur la proposition qui vient de se terminer.
                if (!_dunePathEdited)
                {
                    _dunePath = proposed;
                }
            }
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
            string path = value ?? "";
            if (_dunePath != path)
            {
                _dunePathEdited = path.Length > 0;
            }
            _dunePath = path;
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
    public string TypeLabel =>
        Type == 0
            ? Texts.GetString("63")
            : Texts.GetString("64");

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
    public IReadOnlyList<string> Types { get; }

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
        _folder with
        {
            Path = Path,
            DunePath = DunePath,
            Type = Type,
        };
}
