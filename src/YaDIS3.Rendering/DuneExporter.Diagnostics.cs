using Microsoft.Extensions.Logging;

namespace YaDIS3.Rendering;

public sealed partial class DuneExporter
{
    private const int ExportFailureEventId = 1;

    // Le générateur conserve un message structuré sans allocation par échec.
    [LoggerMessage(
        ExportFailureEventId,
        LogLevel.Error,
        "Export Dune interrompu vers {Destination}"
    )]
    private partial void ExportFailed(
        string destination,
        Exception exception
    );
}
