namespace YaDIS3.Desktop.ViewModels;

public sealed partial class ConfigurationViewModel
{
    /// <summary>
    /// Enregistre uniquement la langue confirmée, sans appliquer
    /// les autres changements provisoires de Configurer.
    /// </summary>
    public void ConfirmLanguageChange(bool confirmed)
    {
        if (!HasLanguageChange)
        {
            return;
        }
        if (!confirmed)
        {
            Language = Texts.Current.Code;
            return;
        }
        string workspace = string.IsNullOrEmpty(WorkspacePath)
            ? Path.Combine(
                Environment.CurrentDirectory,
                "workspace"
            )
            : WorkspacePath;
        _configurationStore.SaveSettings(
            workspace,
            _configurationStore.LoadSettings() with
            {
                Language = Language,
            }
        );
        _restart.Restart();
    }
}
