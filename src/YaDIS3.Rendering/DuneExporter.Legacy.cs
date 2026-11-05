namespace YaDIS3.Rendering;

public sealed partial class DuneExporter
{
    /// <summary>
    /// Reprend le parcours de ProcessForm : menus dans le workspace
    /// puis synchronisation directe vers le dossier de destination.
    /// </summary>
    public ExportResult ExportLegacy(
        IReadOnlyList<MediaContext> cards,
        string destination,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        if (options.BasicMode)
        {
            throw new InvalidOperationException(
                "Le mode basique n'utilise pas l'export central."
            );
        }

        if (!Directory.Exists(destination))
        {
            throw new DirectoryNotFoundException(destination);
        }

        MediaContext[] media = cards
            .Where(card => card.Header.ValueAt("status") != "3")
            .ToArray();
        if (media.Length == 0)
        {
            throw new InvalidOperationException(
                "Aucune fiche traitée à exporter."
            );
        }

        string workspace = Path.GetDirectoryName(
            Path.GetDirectoryName(media[0].DataDirectory)
        )!;
        string source = Path.Combine(workspace, "interface");
        Directory.CreateDirectory(source);

        var warnings = new List<string>();
        MediaPlaybackPlan[] plans = media
            .Select(card => Plan(card, warnings))
            .ToArray();
        foreach (MediaPlaybackPlan plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = Relative(plan.Media)
                .Replace('/', Path.DirectorySeparatorChar);
            string output = Path.Combine(source, relative);
            string cache = Path.Combine(
                workspace,
                "interface-yaDIS3",
                plan.Media.Id
            );
            if (!Directory.Exists(output) && Directory.Exists(cache))
            {
                LegacyProcessedMediaPublisher.Publish(
                    plan.Media,
                    workspace,
                    cache
                );
            }

            if (!File.Exists(Path.Combine(output, "dune_folder.txt")))
            {
                WriteMedia(plan, output);
            }
        }

        try
        {
            using var renderer = new TemplateRenderer(template, options);
            WriteIndexes(renderer, media, source, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            warnings.Add(exception.Message);
        }

        progress?.Report(_texts["122"]);
        new LegacyExportSynchronizer().Synchronize(
            source,
            destination,
            cancellationToken
        );
        return new(
            destination,
            media.Length,
            plans.Sum(plan => plan.Episodes.Count),
            warnings
        );
    }
}
