using System.Text;

namespace YaDIS3.Core;

/// <summary>Stocke les exports sur le système de fichiers local ou monté.</summary>
public sealed class ExportFileSystem : IExportFileSystem
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public bool DirectoryExists(string path) =>
        Directory.Exists(path);

    /// <inheritdoc />
    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    /// <inheritdoc />
    public string ReadText(string path) =>
        File.ReadAllText(path, Encoding.UTF8);

    /// <inheritdoc />
    public void WriteBytes(string path, byte[] bytes) =>
        File.WriteAllBytes(path, bytes);

    /// <inheritdoc />
    public void CopyFile(
        string source,
        string destination,
        bool overwrite
    ) => File.Copy(source, destination, overwrite);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(
        string path,
        string pattern = "*"
    ) => Directory.EnumerateDirectories(path, pattern);
}
