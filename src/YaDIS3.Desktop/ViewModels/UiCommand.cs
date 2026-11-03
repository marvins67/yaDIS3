using System.Windows.Input;

namespace YaDIS3.Desktop.ViewModels;

internal sealed class UiCommand(Func<Task> action) : ICommand
{
    private bool _running;

    /// <summary>
    /// Signale aux boutons que la disponibilité de la commande a
    /// changé.
    /// </summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Indique si la commande peut être lancée.
    /// </summary>
    public bool CanExecute(object? parameter)
    {
        return !_running;
    }

    /// <summary>
    /// Exécute la commande en empêchant deux lancements
    /// simultanés.
    /// </summary>
    public async void Execute(object? parameter)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await action();
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
