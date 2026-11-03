namespace YaDIS3.Desktop.ViewModels;

/// <summary>
/// Case à cocher indépendante du document actif.
/// </summary>
public sealed class ConfigurationChoice(
    string name,
    bool enabled
) : ObservableViewModel
{
    private bool _enabled = enabled;

    /// <summary>
    /// Nom affiché sans renommer l'entrée XML.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Activation proposée.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Notify();
        }
    }
}
