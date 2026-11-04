using System.Windows.Input;
using Avalonia.Media.Imaging;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Vignette du catalogue, indépendante de l'image choisie.</summary>
public sealed class ArtworkChoiceViewModel(
    ArtworkReference reference,
    Func<Task> select,
    string loadingText
) : ObservableViewModel, IDisposable
{
    private Bitmap? _thumbnail;
    private bool _selected;
    private bool _failed;
    private string _hint = loadingText;

    /// <summary>URL originale conservée dans data.xml.</summary>
    public ArtworkReference Reference { get; } = reference;

    /// <summary>Importe l'original dans le brouillon.</summary>
    public ICommand SelectCommand { get; } = new UiCommand(select);

    /// <summary>Largeur historique des affiches et fanarts.</summary>
    public double ImageWidth =>
        Reference.Kind == ArtworkKind.Poster ? 126 : 300;

    /// <summary>Vignette téléchargée, jamais utilisée pour le rendu.</summary>
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        internal set
        {
            _thumbnail?.Dispose();
            _thumbnail = value;
            Notify();
            Notify(nameof(HasThumbnail));
            Notify(nameof(ShowLoading));
            Notify(nameof(ShowError));
        }
    }

    /// <summary>Évite une vignette vide pendant le chargement.</summary>
    public bool HasThumbnail => Thumbnail is not null;

    /// <summary>Affiche l'icône historique de téléchargement.</summary>
    public bool ShowLoading => !HasThumbnail && !_failed;

    /// <summary>Affiche l'icône historique d'image en erreur.</summary>
    public bool ShowError => !HasThumbnail && _failed;

    internal void MarkFailed()
    {
        _failed = true;
        Notify(nameof(ShowLoading));
        Notify(nameof(ShowError));
    }

    /// <summary>Chargement ou diagnostic du téléchargement.</summary>
    public string Hint
    {
        get => _hint;
        internal set
        {
            _hint = value;
            Notify();
        }
    }

    /// <summary>Met en évidence le choix de cette session.</summary>
    public bool IsSelected
    {
        get => _selected;
        internal set
        {
            _selected = value;
            Notify();
        }
    }

    /// <inheritdoc />
    public void Dispose() => Thumbnail = null;
}
