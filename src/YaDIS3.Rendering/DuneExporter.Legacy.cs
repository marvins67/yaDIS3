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
        // L'export ne traite pas les médias : seules les fiches publiées
        // par Traiter peuvent fournir leurs images.
        foreach (MediaPlaybackPlan plan in plans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestoreProcessedMedia(plan.Media, source, warnings);
        }

        PrepareLegacyMenus(source);
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
            warnings,
            cancellationToken
        );
        return new(
            destination,
            media.Length,
            plans.Sum(plan => plan.Episodes.Count),
            warnings
        );
    }

    private static void RestoreProcessedMedia(
        MediaContext media,
        string source,
        List<string> warnings
    )
    {
        string output = Path.Combine(
            source,
            Relative(media).Replace('/', Path.DirectorySeparatorChar)
        );
        if (media.IsSeries)
        {
            return;
        }
        if (CompleteMovie(output))
        {
            return;
        }
        QuarantineEmptyMovie(output);
        warnings.Add($"Fiche à traiter pour générer ses images : {media.Title}");
    }

    private static void QuarantineEmptyMovie(string output)
    {
        if (!Directory.Exists(output)
            || File.Exists(Path.Combine(output, "icon.aai"))
            || File.Exists(Path.Combine(output, "background.jpg"))
            || File.Exists(Path.Combine(output, "background.aai")))
        {
            return;
        }
        // Conserver les fichiers pour diagnostic, sans synchroniser une
        // coquille produite par un ancien export vers la Dune.
        string parent = Path.GetDirectoryName(output)!;
        string hidden = Path.Combine(parent, "_" + Path.GetFileName(output));
        if (Directory.Exists(hidden) || File.Exists(hidden))
        {
            throw new IOException($"Fiche incomplète déjà mise à l'écart : {hidden}");
        }
        Directory.Move(output, hidden);
    }

    private static bool CompleteMovie(string directory) =>
        File.Exists(Path.Combine(directory, "dune_folder.txt"))
        && File.Exists(Path.Combine(directory, "icon.aai"))
        && (File.Exists(Path.Combine(directory, "background.jpg"))
            || File.Exists(Path.Combine(directory, "background.aai")));

    private static void PrepareLegacyMenus(string root)
    {
        TryRemoveMenu(Path.Combine(root, "00", "03"));
        TryRemoveMenu(Path.Combine(root, "00", "98"));
        foreach (string pattern in new[] { "02_*", "05_*" })
        {
            foreach (string directory in Directory.GetDirectories(root, pattern))
            {
                TryRemoveMenu(directory);
            }
        }
    }

    private static void TryRemoveMenu(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException)
        {
            // ProcessForm ignore ces échecs pendant la préparation des menus.
        }
    }
}
