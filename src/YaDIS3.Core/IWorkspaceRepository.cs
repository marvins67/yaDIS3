namespace YaDIS3.Core;

/// <summary>Permet de substituer le stockage du workspace dans les traitements.</summary>
public interface IWorkspaceRepository
{
    /// <summary>Charge les fiches existantes.</summary>
    Workspace Open(
        string path,
        CancellationToken cancellationToken = default
    );

    /// <summary>Crée un workspace vide.</summary>
    void Create(string path);

    /// <summary>Crée une fiche pour une série locale.</summary>
    MediaCard CreateManualSeries(
        Workspace workspace,
        string videoDirectory,
        IReadOnlyList<VideoEpisode>? detected = null
    );
}
