using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>Accès aux informations nécessaires au traitement.</summary>
public interface ITreatmentMetadata
{
    /// <summary>Identifie ou recharge une série.</summary>
    Task<SeriesDetails?> FindSeriesAsync(
        MediaCard card,
        CancellationToken cancellationToken
    );

    /// <summary>Identifie ou recharge un film.</summary>
    Task<XElement?> FindMovieAsync(
        MediaCard card,
        CancellationToken cancellationToken
    );

    /// <summary>Complète les illustrations sans remplacer les imports.</summary>
    Task CompleteArtworkAsync(
        MediaCard card,
        CancellationToken cancellationToken
    );
}
