using System.IO.Compression;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

/// <summary>Entrées réelles de l'archive yaDIS2, sans modification des octets de référence.</summary>
internal sealed class LegacyXmlCorpus : IDisposable
{
    internal string Root { get; } = Path.Combine(
        Environment.GetEnvironmentVariable("YADIS_XML_AUDIT_DIR") ?? Path.GetTempPath(),
        "xml-regenerated-" + Guid.NewGuid().ToString("N"));
    internal Dictionary<string, byte[]> Documents { get; } = Read();
    internal string UserConfig => Path.Combine(Root, "user.config");

    private static Dictionary<string, byte[]> Read()
    {
        using ZipArchive archive = ZipFile.OpenRead(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "LegacyXml", "yadis2-xml-reference.zip"));
        return archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .ToDictionary(entry => entry.FullName, entry =>
            {
                using Stream input = entry.Open();
                using MemoryStream output = new();
                input.CopyTo(output);
                return output.ToArray();
            }, StringComparer.Ordinal);
    }

    internal MediaSourceFolder[] Folders()
    {
        using MemoryStream input = new(Documents["folders.xml"]);
        return XDocument.Load(input).Descendants("folder").Select(folder => new MediaSourceFolder(
            folder.Element("path")!.Value, int.Parse(folder.Element("type")!.Value,
                System.Globalization.CultureInfo.InvariantCulture), folder.Element("dunepath")!.Value)).ToArray();
    }

    internal IEnumerable<XElement> Items() => Documents.Where(pair => pair.Key.EndsWith("/data.xml", StringComparison.Ordinal))
        .Select(pair =>
        {
            using MemoryStream input = new(pair.Value);
            return XDocument.Load(input).Root!.Element("item")!;
        });

    internal byte[] Actual(string relative) => File.ReadAllBytes(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("YADIS_XML_AUDIT_DIR") is null && Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
