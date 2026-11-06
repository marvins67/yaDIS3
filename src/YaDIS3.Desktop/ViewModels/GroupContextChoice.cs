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

    /// <summary>État mixte de l'appartenance en sélection multiple.</summary>
    public bool IsIndeterminate { get; init; }

    /// <summary>Actions supplémentaires accessibles par sous-menu.</summary>
    public IReadOnlyList<GroupContextChoice> Children { get; init; } = [];

    /// <summary>Action associée à l'entrée.</summary>
    public ICommand SelectCommand { get; } = selectCommand;
}
