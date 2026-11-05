namespace YaDIS3.Rendering;

/// <summary>
/// Signale un échec qui interrompt le rendu de la sélection.
/// </summary>
public sealed class TreatmentRenderException(
    string message,
    Exception innerException
) : IOException(message, innerException);
