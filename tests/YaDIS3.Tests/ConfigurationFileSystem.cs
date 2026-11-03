using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class ConfigurationFileSystem(int failOnWrite)
    : IExportFileSystem
{
    private readonly ExportFileSystem _files = new();
    private int _writes;

    public bool FileExists(string path) =>
        _files.FileExists(path);

    public bool DirectoryExists(string path) =>
        _files.DirectoryExists(path);

    public void CreateDirectory(string path) =>
        _files.CreateDirectory(path);

    public string ReadText(string path) => _files.ReadText(path);

    public byte[] ReadBytes(string path) =>
        _files.ReadBytes(path);

    public void WriteBytes(string path, byte[] bytes)
    {
        if (++_writes == failOnWrite)
        {
            _files.WriteBytes(path, [1, 2]);
            throw new IOException(
                "Écriture interrompue : " + path
            );
        }
        _files.WriteBytes(path, bytes);
    }

    public void CopyFile(
        string source,
        string destination,
        bool overwrite
    ) => _files.CopyFile(source, destination, overwrite);

    public void MoveFile(
        string source,
        string destination,
        bool overwrite
    ) => _files.MoveFile(source, destination, overwrite);

    public void MoveDirectory(
        string source,
        string destination
    ) => _files.MoveDirectory(source, destination);

    public FileAttributes GetAttributes(string path) =>
        _files.GetAttributes(path);

    public void DeleteFile(string path) =>
        _files.DeleteFile(path);

    public void DeleteDirectory(string path) =>
        _files.DeleteDirectory(path);

    public IEnumerable<string> EnumerateFiles(string path) =>
        _files.EnumerateFiles(path);

    public IEnumerable<string> EnumerateDirectories(
        string path,
        string pattern = "*"
    ) => _files.EnumerateDirectories(path, pattern);
}
