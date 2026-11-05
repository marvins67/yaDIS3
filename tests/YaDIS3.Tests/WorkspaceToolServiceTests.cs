using Microsoft.Extensions.Logging.Abstractions;
using YaDIS3.Core;
using Xunit;

namespace YaDIS3.Tests;

/// <summary>
/// Caractérise les effets du bouton Outils historique sur le workspace.
/// </summary>
public sealed class WorkspaceToolServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "yadis3-tools-" + Guid.NewGuid().ToString("N")
    );
    private readonly WorkspaceToolService _service = new(
        NullLogger.Instance
    );

    [Fact]
    public void FlushImageCache_FilesAtRoot_DeletesOnlyRootFiles()
    {
        string cache = Directory.CreateDirectory(
            Path.Combine(_root, "cache")
        ).FullName;
        string nested = Directory.CreateDirectory(
            Path.Combine(cache, "nested")
        ).FullName;
        string rootFile = Write(cache, "cover.jpg");
        string nestedFile = Write(nested, "keep.jpg");

        _service.FlushImageCache(_root);

        Assert.False(File.Exists(rootFile));
        Assert.True(File.Exists(nestedFile));
        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void FlushImageCache_EmptyCache_CompletesWithoutChanges()
    {
        string cache = Directory.CreateDirectory(
            Path.Combine(_root, "cache")
        ).FullName;

        _service.FlushImageCache(_root);

        Assert.Empty(Directory.GetFileSystemEntries(cache));
    }

    [Fact]
    public void FlushImageCache_MissingCache_DoesNotThrow()
    {
        Directory.CreateDirectory(_root);

        _service.FlushImageCache(_root);

        Assert.False(Directory.Exists(Path.Combine(_root, "cache")));
    }

    [Fact]
    public void ResetDuneInterface_NestedContent_DeletesContentsButKeepsRoot()
    {
        string directory = Directory.CreateDirectory(
            Path.Combine(_root, "interface")
        ).FullName;
        string nested = Directory.CreateDirectory(
            Path.Combine(directory, "movie", "season")
        ).FullName;
        string rootFile = Write(directory, "dune_folder.txt");
        string nestedFile = Write(nested, "preview.jpg");

        _service.ResetDuneInterface(_root);

        Assert.True(Directory.Exists(directory));
        Assert.Empty(Directory.GetFileSystemEntries(directory));
        Assert.False(File.Exists(rootFile));
        Assert.False(File.Exists(nestedFile));
    }

    [Fact]
    public void ResetDuneInterface_MissingDirectory_DoesNotThrow()
    {
        Directory.CreateDirectory(_root);

        _service.ResetDuneInterface(_root);

        Assert.False(Directory.Exists(Path.Combine(_root, "interface")));
    }

    [Fact]
    public void ResetDuneInterface_EmptyDirectory_PreservesRoot()
    {
        string directory = Directory.CreateDirectory(
            Path.Combine(_root, "interface")
        ).FullName;

        _service.ResetDuneInterface(_root);

        Assert.True(Directory.Exists(directory));
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public void Tools_ExistingMediaAndPreview_DoNotModifySourceData()
    {
        string media = Directory.CreateDirectory(
            Path.Combine(_root, "data", "movie")
        ).FullName;
        string metadata = Write(media, "data.xml");
        string preview = Write(media, "preview.jpg");
        string poster = Write(media, "poster.jpg");
        string cache = Directory.CreateDirectory(
            Path.Combine(_root, "cache")
        ).FullName;
        string dune = Directory.CreateDirectory(
            Path.Combine(_root, "interface")
        ).FullName;
        Write(cache, "cached.jpg");
        Write(dune, "dune_folder.txt");
        byte[] metadataBefore = File.ReadAllBytes(metadata);
        byte[] previewBefore = File.ReadAllBytes(preview);
        byte[] posterBefore = File.ReadAllBytes(poster);

        _service.FlushImageCache(_root);
        _service.ResetDuneInterface(_root);

        Assert.Equal(metadataBefore, File.ReadAllBytes(metadata));
        Assert.Equal(previewBefore, File.ReadAllBytes(preview));
        Assert.Equal(posterBefore, File.ReadAllBytes(poster));
        Assert.Empty(Directory.GetFileSystemEntries(cache));
        Assert.Empty(Directory.GetFileSystemEntries(dune));
    }

    [Fact]
    public void Tools_CachePathIsFile_DoesNotPreventInterfaceReset()
    {
        Directory.CreateDirectory(_root);
        string cachePath = Write(_root, "cache");
        string dune = Directory.CreateDirectory(
            Path.Combine(_root, "interface")
        ).FullName;
        string generated = Write(dune, "preview.jpg");

        _service.FlushImageCache(_root);
        _service.ResetDuneInterface(_root);

        Assert.True(File.Exists(cachePath));
        Assert.False(File.Exists(generated));
    }

    [Fact]
    public void Tools_NullOrBlankWorkspace_RejectsArgument()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.FlushImageCache(null!)
        );
        Assert.Throws<ArgumentException>(() =>
            _service.ResetDuneInterface(" ")
        );
    }

    private static string Write(string directory, string filename)
    {
        string path = Path.Combine(directory, filename);
        File.WriteAllText(path, "unchanged");
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
