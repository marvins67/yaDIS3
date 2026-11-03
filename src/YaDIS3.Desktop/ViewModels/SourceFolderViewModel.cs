using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Copie éditable d'un dossier, validée avant insertion dans la liste.</summary>
public sealed class SourceFolderViewModel(
    MediaSourceFolder folder
) : ObservableViewModel
{
    private string _path = folder.Path;
    private string _dunePath = folder.DunePath;
    private int _type = folder.Type;
    private bool _basicMode;

    /// <summary>Dossier local ou volume monté.</summary>
    public string Path
    {
        get => _path;
        set
        {
            _path = value;
            Notify(null);
        }
    }

    /// <summary>Chemin utilisé par le Dune.</summary>
    public string DunePath
    {
        get => _dunePath;
        set
        {
            _dunePath = value;
            Notify(null);
        }
    }

    /// <summary>Films (0) ou séries (1).</summary>
    public int Type
    {
        get => _type;
        set
        {
            _type = value;
            Notify(null);
        }
    }

    /// <summary>Type affiché dans la liste des dossiers.</summary>
    public string TypeLabel => Type == 0 ? "Films" : "Séries TV";

    /// <summary>Libellés du sélecteur de type.</summary>
    public IReadOnlyList<string> Types { get; } =
    ["Films", "Séries TV"];

    /// <summary>Le mode basique ne demande pas de chemin réseau.</summary>
    public bool BasicMode
    {
        get => _basicMode;
        set
        {
            _basicMode = value;
            Notify(null);
        }
    }

    /// <summary>Validation d'AddFolderForm reprise du programme original.</summary>
    public bool CanAccept =>
        Path.Length > 0 && (BasicMode || DunePath.Length > 0);

    /// <summary>Conserve les extensions XML du dossier original.</summary>
    public MediaSourceFolder Snapshot() =>
        folder with
        {
            Path = Path,
            DunePath = DunePath,
            Type = Type,
        };
}
