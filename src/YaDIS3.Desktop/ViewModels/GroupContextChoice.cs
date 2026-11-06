using System.Windows.Input;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Entrée de saga ou sélection pour le menu contextuel.</summary>
public sealed class GroupContextChoice(
    string label,
    bool isChecked,
    ICommand selectCommand
)
{
    /// <summary>Nom affiché dans le sous-menu.</summary>
    public string Label { get; } = label;

    /// <summary>Appartenance actuelle du film.</summary>
    public bool IsChecked { get; } = isChecked;

    /// <summary>Action associée à l'entrée.</summary>
    public ICommand SelectCommand { get; } = selectCommand;
}
