using System.Text;
using System.Xml;
using Xunit;

namespace YaDIS3.Tests;

/// <summary>Oracle issu du code historique pour les scénarios absents des archives.</summary>
internal static class LegacyXmlReference
{
    internal const string Declaration = "<?xml version='1.0' encoding='utf-8'?>";
    internal const string Groups = "<groups><sagas></sagas><selections></selections></groups>";

    internal static XmlDocument Document(string xml)
    {
        XmlDocument document = new() { XmlResolver = null };
        document.LoadXml(Declaration + xml);
        return document;
    }

    internal static byte[] Bytes(XmlDocument document)
    {
        using MemoryStream stream = new();
        Encoding encoding = document.FirstChild is XmlDeclaration { Encoding.Length: > 0 } declaration
            ? Encoding.GetEncoding(declaration.Encoding) : new UTF8Encoding(false);
        // Save(filename) emploie XmlTextWriter. Ce TextWriter fixe les fins de
        // ligne Windows pour exécuter l'oracle sur les autres systèmes.
        using (StreamWriter text = new(stream, encoding, leaveOpen: true) { NewLine = "\r\n" })
        using (XmlTextWriter writer = new(text) { Formatting = Formatting.Indented })
        {
            document.WriteTo(writer);
        }
        return stream.ToArray();
    }

    internal static void Set(XmlDocument document, string xpath, string value) => document.SelectSingleNode(xpath)!.InnerText = value;

    internal static void AssertFile(XmlDocument expected, string path)
    {
        byte[] actual = File.ReadAllBytes(path);
        Assert.Equal(Bytes(expected), actual);
        XmlDocument reread = new();
        reread.Load(path);
        XmlDocument parsedExpected = new();
        using MemoryStream reference = new(Bytes(expected));
        parsedExpected.Load(reference);
        Assert.Equal(parsedExpected.DocumentElement!.OuterXml, reread.DocumentElement!.OuterXml);
    }
}
