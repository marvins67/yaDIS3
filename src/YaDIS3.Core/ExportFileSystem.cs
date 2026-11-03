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
    public byte[] ReadBytes(string path) =>
        File.ReadAllBytes(path);

    /// <inheritdoc />
    public void DeleteFile(string path) => File.Delete(path);

    /// <inheritdoc />
    public void MoveFile(
        string source,
        string destination,
        bool overwrite
    ) => File.Move(source, destination, overwrite);

    /// <inheritdoc />
    public void MoveDirectory(
        string source,
        string destination
    ) => Directory.Move(source, destination);

    /// <inheritdoc />
    public FileAttributes GetAttributes(string path) =>
        File.GetAttributes(path);

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
    public void DeleteDirectory(string path) =>
        Directory.Delete(path, true);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string path) =>
        Directory.EnumerateFiles(path);

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectories(
        string path,
        string pattern = "*"
    ) => Directory.EnumerateDirectories(path, pattern);
}
