namespace YaDIS3.Desktop.Localization;

/// <summary>Permet de vérifier le redémarrage sans lancer un second processus.</summary>
public interface IApplicationRestart
{
    /// <summary>Relance l'application avec ses arguments actuels.</summary>
    void Restart();
}
