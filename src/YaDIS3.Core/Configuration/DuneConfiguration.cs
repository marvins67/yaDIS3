namespace YaDIS3.Core;

/// <summary>
/// Préférences Dune ; les valeurs historiques du portage restent
/// inchangées.
/// </summary>
public sealed record DuneConfiguration
{
    /// <summary>Workspace sélectionné dans user.config.</summary>
    public string Workspace { get; init; } = "";

    /// <summary>
    /// Thème sélectionné.
    /// </summary>
    public string Template { get; init; } = "Origins";

    /// <summary>
    /// Dossier d'un thème externe.
    /// </summary>
    public string TemplatePath { get; init; } = "";

    /// <summary>
    /// Langue des libellés des synopsis.
    /// </summary>
    public string Language { get; init; } = "fr";

    /// <summary>
    /// Fonds au format AAI plutôt que JPEG.
    /// </summary>
    public bool Aai { get; init; }

    /// <summary>
    /// Boîtiers des films.
    /// </summary>
    public bool Cases { get; init; }

    /// <summary>
    /// Destination de l'interface centrale.
    /// </summary>
    public string Destination { get; init; } = "";

    /// <summary>
    /// Interface déposée directement auprès des vidéos.
    /// </summary>
    public bool BasicMode { get; init; }

    /// <summary>
    /// Qualité JPEG de 1 à 100 ; 90 reprend la valeur
    /// par défaut de yaDIS2.
    /// </summary>
    public int JpegQuality { get; init; } = 90;

    /// <summary>
    /// Ajouts récents puis catégories 03 à 11.
    /// </summary>
    public string Categories { get; init; } = "11111111111";

    /// <summary>
    /// Période des ajouts récents.
    /// </summary>
    public int LastDays { get; init; } = 60;

    /// <summary>
    /// Tri des nouveautés selon leur ancienneté.
    /// </summary>
    public bool SortRecentByDate { get; init; }

    /// <summary>
    /// Cadre de sélection du Dune.
    /// </summary>
    public bool SelectionBox { get; init; } = true;

    /// <summary>
    /// Barre de défilement du Dune.
    /// </summary>
    public bool Scrollbar { get; init; } = true;

    /// <summary>
    /// Articles ignorés pour établir un nouveau titre de tri.
    /// </summary>
    public bool IgnoreArticles { get; init; }

    /// <summary>
    /// Liste d'articles séparés par des virgules.
    /// </summary>
    public string Articles { get; init; } =
        "the,a,an,la,le,les,l',un,une,des";

    /// <summary>
    /// Ajout automatique des nouveaux films traités.
    /// </summary>
    public bool AutoSelect { get; init; }

    /// <summary>
    /// Sélection recevant les nouveaux films.
    /// </summary>
    public string AutoSelectName { get; init; } = "";

    /// <summary>
    /// Fichier de configuration TVDB existant, sans recopier ses
    /// secrets.
    /// </summary>
    public string TvdbPath { get; init; } = "";
}
