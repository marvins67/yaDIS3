using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace YaDIS3.Core;

/// <summary>
/// Reprend XmlDocument.Save de yaDIS2 pour les XML du workspace.
/// Les préférences user.config suivent un mécanisme historique distinct.
/// </summary>
internal static class LegacyWorkspaceXml
{
    internal static XDocument Create(XElement root) =>
        new(new XDeclaration("1.0", "utf-8", null), root);

    internal static byte[] Serialize(XDocument source)
    {
        XmlDocument document = ToDocument(source);
        Encoding encoding = source.Declaration is { Encoding.Length: > 0 } declaration
            ? Encoding.GetEncoding(declaration.Encoding)
            : new UTF8Encoding(false);
        using MemoryStream stream = new();
        // XmlDocument.Save(filename) utilise cet écrivain. Le TextWriter fixe
        // les fins de ligne de Windows indépendamment de l'OS du portage.
        using (StreamWriter text = new(stream, encoding, leaveOpen: true) { NewLine = "\r\n" })
        using (XmlTextWriter writer = new(text) { Formatting = Formatting.Indented })
        {
            document.WriteTo(writer);
        }
        return stream.ToArray();
    }

    private static XmlDocument ToDocument(XDocument source)
    {
        // Les lecteurs du workspace retirent déjà l'indentation comme le DOM
        // historique. Les textes restant dans LINQ sont donc significatifs,
        // y compris les espaces ou CR saisis par l'utilisateur.
        using XmlReader reader = source.CreateReader();
        XmlDocument document = new() { XmlResolver = null };
        document.Load(reader);
        // Le lecteur LINQ n'expose pas la déclaration du XDocument.
        if (source.Declaration is { } declaration)
        {
            document.InsertBefore(document.CreateXmlDeclaration(
                declaration.Version ?? "1.0", declaration.Encoding, declaration.Standalone), document.FirstChild);
        }
        return document;
    }
}
