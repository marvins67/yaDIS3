using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

/// <summary>
/// Simule uniquement la disponibilité des quatre sources Windows de l'archive.
/// Toutes les écritures de yaDIS3 restent réelles, dans le workspace du test.
/// </summary>
internal sealed class LegacyXmlSourceFileSystem : IExportFileSystem
{
    private readonly ExportFileSystem _disk = new();
    private readonly Dictionary<string, List<(string Path, bool Folder)>> _sources = new(StringComparer.Ordinal);

    internal LegacyXmlSourceFileSystem(LegacyXmlCorpus corpus)
    {
        foreach (MediaSourceFolder folder in corpus.Folders())
        {
            _sources.Add(folder.Path, []);
        }
        foreach (XElement item in corpus.Items())
        {
            string parent = item.Element("path")!.Value;
            _sources[parent].Add((Path.Combine(parent, item.Element("filename")!.Value), item.Element("folder")!.Value == "yes"));
        }
    }

    public bool FileExists(string path) => _sources.Values.SelectMany(items => items).Any(item => item.Path == path && !item.Folder) || _disk.FileExists(path);
    public bool DirectoryExists(string path) => _sources.ContainsKey(path) || _sources.Values.SelectMany(items => items).Any(item => item.Path == path && item.Folder) || _disk.DirectoryExists(path);
    public void CreateDirectory(string path) => _disk.CreateDirectory(path);
    public string ReadText(string path) => _disk.ReadText(path);
    public byte[] ReadBytes(string path) => _disk.ReadBytes(path);
    public void WriteBytes(string path, byte[] bytes) => _disk.WriteBytes(path, bytes);
    public void DeleteFile(string path) => _disk.DeleteFile(path);
    public void MoveFile(string source, string destination, bool overwrite) => _disk.MoveFile(source, destination, overwrite);
    public void MoveDirectory(string source, string destination) => _disk.MoveDirectory(source, destination);
    public void CopyFile(string source, string destination, bool overwrite) => _disk.CopyFile(source, destination, overwrite);
    public void DeleteDirectory(string path) => _disk.DeleteDirectory(path);
    public FileAttributes GetAttributes(string path) => FileAttributes.Normal;
    public IEnumerable<string> EnumerateFiles(string path) => _sources.TryGetValue(path, out var items)
        ? items.Where(item => !item.Folder).Select(item => item.Path) : _disk.EnumerateFiles(path);
    public IEnumerable<string> EnumerateDirectories(string path, string pattern = "*") => _sources.TryGetValue(path, out var items)
        ? items.Where(item => item.Folder).Select(item => item.Path) : _disk.EnumerateDirectories(path, pattern);
}
