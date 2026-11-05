using YaDIS3.Core;

namespace YaDIS3.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Vide uniquement les fichiers à la racine du cache historique.
    /// </summary>
    public async Task FlushImageCacheAsync()
    {
        string workspace = _workspace?.Directory
            ?? throw new InvalidOperationException(Texts.GetString("352"));
        await Task.Run(() =>
            new WorkspaceToolService(_logger).FlushImageCache(workspace)
        );
        RefreshImages();
    }

    /// <summary>
    /// Supprime les fichiers et les sous-dossiers de l'interface Dune.
    /// </summary>
    public async Task ResetDuneInterfaceAsync()
    {
        string workspace = _workspace?.Directory
            ?? throw new InvalidOperationException(Texts.GetString("352"));
        await Task.Run(() =>
            new WorkspaceToolService(_logger).ResetDuneInterface(workspace)
        );
        RefreshImages();
    }
}
