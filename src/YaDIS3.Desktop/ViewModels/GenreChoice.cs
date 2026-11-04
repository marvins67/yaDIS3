namespace YaDIS3.Desktop.ViewModels;

/// <summary>État d'une case du menu des genres.</summary>
public sealed class GenreChoice(string name, bool selected)
{
    /// <summary>Genre tel qu'enregistré dans les fiches.</summary>
    public string Name { get; } = name;

    /// <summary>Sélection provisoire validée à la fermeture du menu.</summary>
    public bool IsSelected { get; set; } = selected;
}
