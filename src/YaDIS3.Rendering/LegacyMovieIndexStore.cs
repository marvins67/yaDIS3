using System.Buffers.Binary;
using System.Globalization;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>
/// Conserve les index de films dans workspace/interface comme
/// yaDIS2.
/// </summary>
public sealed class LegacyMovieIndexStore(
    DuneTemplate template,
    RenderOptions options,
    IExportFileSystem? fileSystem = null
)
{
    private readonly IExportFileSystem _fileSystem =
        fileSystem ?? new ExportFileSystem();

    /// <summary>
    /// Reprend AddToIndex après le rendu d'un film, sans
    /// reconstruire les listes.
    /// </summary>
    public void Update(
        MediaContext movie,
        string workspace,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(movie);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        cancellationToken.ThrowIfCancellationRequested();
        if (movie.IsSeries)
        {
            return;
        }
        try
        {
            UpdateMovie(movie, workspace, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            throw new InvalidDataException(
                $"Mise à jour des index de la fiche « {movie.Title} » ({Path.Combine(movie.DataDirectory, "data.xml")}) : {exception.Message}",
                exception
            );
        }
    }

    private void UpdateMovie(
        MediaContext movie,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<LegacyIndexEntry> entries =
            LegacyMovieIndexes.Create(
                movie.Info,
                movie
                    .Root.Descendants("mediainfo")
                    .FirstOrDefault()
            );
        using TemplateRenderer renderer = new(template, options);
        string root = Path.Combine(workspace, "interface");
        foreach (LegacyIndexEntry entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdateEntry(movie, root, entry, renderer);
        }
    }

    private void UpdateEntry(
        MediaContext movie,
        string root,
        LegacyIndexEntry entry,
        TemplateRenderer renderer
    )
    {
        string directory = ResolveDirectory(
            root,
            entry.Directory
        );
        string path = Path.Combine(directory, "dune_folder.txt");
        string ancestor =
            entry.Directory.Split('/').Length == 4
                ? "../../.."
                : "../..";
        DuneFile file = IndexDocument(path, ancestor);
        string url =
            ancestor + "/99/" + movie.Header.ValueAt("filename");
        file.AddIndexMovie(
            movie.Info.ValueAt("sort_title"),
            url,
            template.Node("movie/iconview/icon").Attr("scale")
        );
        renderer.RenderLegacyIndexLabel(
            directory,
            entry.Caption,
            IndexKind(entry.Directory.Split('/')[1])
        );
        file.Save(directory);
    }

    private DuneFile IndexDocument(string path, string ancestor)
    {
        DuneFile file = DuneFile.FromLegacy(
            new LegacyDuneDefinitions(template).IndexListing(
                ancestor
            ),
            _fileSystem
        );
        file.Set(
            "background_path",
            ancestor
                + "/00/background"
                + (options.AaiBackgrounds ? ".aai" : ".jpg")
        );
        if (_fileSystem.FileExists(path))
        {
            file.ImportIndexItems(_fileSystem.ReadText(path));
        }
        return file;
    }

    private static string IndexKind(string category) =>
        category switch
        {
            "04" => "genres",
            "05" => "actors",
            "06" => "directors",
            "07" => "durations",
            "08" => "countries",
            "09" => "years",
            "10" => "ratings",
            "11" => "types",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category)
            ),
        };

    private string ResolveDirectory(string root, string relative)
    {
        string[] components = relative.Split('/');
        // CreateAlphabet précrée les initiales A à Z sous Windows.
        if (
            components.Length == 4
            && components[2][0] is >= 'a' and <= 'z'
        )
        {
            components[2] = components[2].ToUpperInvariant();
        }
        foreach (string component in components)
        {
            string? existing = _fileSystem.DirectoryExists(root)
                ? _fileSystem
                    .EnumerateDirectories(root)
                    .FirstOrDefault(path =>
                        string.Equals(
                            Path.GetFileName(path),
                            component,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                : null;
            root = existing ?? Path.Combine(root, component);
        }
        return root;
    }

    internal void CopyCategory(
        string workspace,
        string destination,
        int category,
        CancellationToken cancellationToken
    )
    {
        string relative = Path.Combine(
            "00",
            category.ToString("00", CultureInfo.InvariantCulture)
        );
        string source = Path.Combine(
            workspace,
            "interface",
            relative
        );
        string target = Path.Combine(destination, relative);
        if (
            _fileSystem.DirectoryExists(source)
            && !string.Equals(
                Path.GetFullPath(source),
                Path.GetFullPath(target),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            CopyDirectory(source, target, cancellationToken);
        }
    }

    /// <summary>
    /// Répare à l'export les étiquettes historiques dont la hauteur
    /// est périmée, sans reconstruire les listes de navigation.
    /// </summary>
    internal void RefreshOutdatedLabels(
        string destination,
        int category,
        TemplateRenderer renderer,
        CancellationToken cancellationToken
    )
    {
        string root = Path.Combine(
            destination,
            "00",
            category.ToString("00", CultureInfo.InvariantCulture)
        );
        if (!_fileSystem.DirectoryExists(root) || category == 8)
        {
            return;
        }

        string kind = IndexKind(
            category.ToString("00", CultureInfo.InvariantCulture)
        );
        RefreshDirectory(root, kind, renderer, 0, cancellationToken);
    }

    private void RefreshDirectory(
        string directory,
        string kind,
        TemplateRenderer renderer,
        int depth,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(directory, "icon.aai");
        // Les racines de rubriques n'ont pas de variante sélectionnée
        // dans CreateIndexItem (yaDIS2). Elles ne sont pas des libellés.
        bool isLabel = kind is "actors" or "directors"
            ? depth >= 2
            : depth >= 1;
        if (isLabel
            && _fileSystem.FileExists(path)
            && _fileSystem.FileExists(Path.Combine(directory, "icon_sel.aai")))
        {
            byte[] bytes = _fileSystem.ReadBytes(path);
            if (bytes.Length >= 8)
            {
                int width = BinaryPrimitives.ReadInt32LittleEndian(bytes);
                int height = BinaryPrimitives.ReadInt32LittleEndian(
                    bytes.AsSpan(4)
                );
                (int expectedWidth, int expectedHeight) =
                    renderer.LegacyIndexDimensions(Path.GetFileName(directory), kind);
                if (width > 0
                    && height > 0
                    && (long)width * height * 4 + 8 == bytes.Length
                    && (width != expectedWidth || height != expectedHeight))
                {
                    renderer.RenderLegacyIndexLabel(
                        directory,
                        Path.GetFileName(directory),
                        kind
                    );
                }
            }
        }

        foreach (string child in _fileSystem.EnumerateDirectories(directory))
        {
            if (!Path.GetFileName(child).StartsWith('_'))
            {
                RefreshDirectory(child, kind, renderer, depth + 1, cancellationToken);
            }
        }
    }

    private void CopyDirectory(
        string source,
        string destination,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _fileSystem.CreateDirectory(destination);
        foreach (
            string path in _fileSystem.EnumerateFiles(source)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            _fileSystem.CopyFile(
                path,
                Path.Combine(
                    destination,
                    Path.GetFileName(path)
                ),
                true
            );
        }
        foreach (
            string directory in _fileSystem.EnumerateDirectories(
                source
            )
        )
        {
            string name = Path.GetFileName(directory);
            // SyncInterface exclut les dossiers mis à l'écart par yaDIS2.
            if (!name.StartsWith('_'))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, name),
                    cancellationToken
                );
            }
        }
    }
}
