namespace YaDIS3.Core;

/// <summary>Choix du menu Traiter de yaDIS2.</summary>
public enum TreatmentMode
{
    /// <summary>Complète les données manquantes.</summary>
    Normal,

    /// <summary>Recharge les métadonnées du fournisseur.</summary>
    ReloadInformation,

    /// <summary>Relit les caractéristiques des fichiers vidéo.</summary>
    ReloadTechnicalInformation,

    /// <summary>Régénère toutes les illustrations des épisodes.</summary>
    AllEpisodes,
}
