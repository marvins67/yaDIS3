using System.Windows.Input;
using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Entrée de filtre sans dépendance aux contrôles Avalonia.
/// </summary>
public sealed class LibraryFilterItem
{
    /// <summary>Libellé du menu et du bouton sélectionné.</summary>
    public string Label { get; }

    /// <summary>Prédicat associé à cette entrée.</summary>
    public LegacyLibraryFilter Filter { get; }

    /// <summary>Active le filtre dans la fenêtre principale.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>Construit une entrée et sa commande de sélection.</summary>
    public LibraryFilterItem(
        string label,
        LegacyLibraryFilter filter,
        Action<LibraryFilterItem> select
    )
    {
        ArgumentNullException.ThrowIfNull(select);
        Label = label;
        Filter = filter;
        SelectCommand = new UiCommand(() =>
        {
            select(this);
            return Task.CompletedTask;
        });
    }
}
