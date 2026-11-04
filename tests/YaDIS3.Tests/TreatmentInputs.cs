using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class TreatmentInputs
    : ITreatmentMetadata,
        IVideoInformationReader
{
    public int SeriesRequests { get; private set; }

    public int MovieRequests { get; private set; }

    public int TechnicalReads { get; private set; }

    public bool SeriesNotFound { get; init; }

    public bool FailSearch { get; init; }

    public Action? DuringSeriesSearch { get; init; }

    public Task<SeriesDetails?> FindSeriesAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        SeriesRequests++;
        DuringSeriesSearch?.Invoke();
        if (FailSearch)
        {
            throw new HttpRequestException("TVDB indisponible");
        }
        return Task.FromResult<SeriesDetails?>(
            SeriesNotFound
                ? null
                : new(
                    42,
                    "Série identifiée",
                    "Synopsis",
                    "2023-01-01",
                    "Chaîne",
                    [
                        new(
                            1,
                            1,
                            "Épisode identifié",
                            "Résumé",
                            "2023-01-01",
                            ""
                        ),
                    ]
                )
        );
    }

    public Task<XElement?> FindMovieAsync(
        MediaCard card,
        CancellationToken cancellationToken
    )
    {
        MovieRequests++;
        return Task.FromResult<XElement?>(
            new XElement(
                "info",
                new XElement("id", "42"),
                new XElement("title", "Film identifié")
            )
        );
    }

    public Task CompleteArtworkAsync(
        MediaCard card,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public Task<XElement> ReadAsync(
        string file,
        CancellationToken cancellationToken
    )
    {
        TechnicalReads++;
        return Task.FromResult(
            new XElement(
                "mediainfo",
                new XElement("format", "Matroska"),
                new XElement(
                    "video",
                    new XElement("codec", "AVC")
                )
            )
        );
    }
}
