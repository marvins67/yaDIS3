using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YaDIS3.Desktop.ViewModels;

/// <summary>Notifications communes aux copies de travail de la configuration.</summary>
public abstract class ObservableViewModel
    : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Signale une propriété, ou toutes ses valeurs dérivées.</summary>
    protected void Notify(
        [CallerMemberName] string? name = null
    ) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name)
        );
}
