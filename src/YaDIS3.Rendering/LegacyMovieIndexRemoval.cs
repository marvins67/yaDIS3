using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Rendering;

/// <summary>Reprend RemoveFromIndex lors de l'enregistrement d'une fiche modifiée.</summary>
internal sealed class LegacyMovieIndexRemoval(
    IExportFileSystem fileSystem
)
{
    public void Remove(
        XElement root,
        string workspace,
        CancellationToken cancellationToken
    )
    {
        XElement header =
            root.Name == "item" ? root : root.Element("item")!;
        XElement? info =
            root.Element("info") ?? header.Element("info");
        if (header.ValueAt("type") != "0" || info is null)
        {
            return;
        }
        foreach (
            LegacyIndexEntry entry in LegacyMovieIndexes.Create(
                info,
                root.Descendants("mediainfo").FirstOrDefault()
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = FindDirectory(
                Path.Combine(workspace, "interface"),
                entry.Directory
            );
            RemoveEntry(directory, info.ValueAt("sort_title"));
        }
    }

    private string FindDirectory(string root, string relative)
    {
        foreach (string name in relative.Split('/'))
        {
            if (!fileSystem.DirectoryExists(root))
            {
                return Path.Combine(root, name);
            }
            root =
                fileSystem
                    .EnumerateDirectories(root)
                    .FirstOrDefault(path =>
                        string.Equals(
                            Path.GetFileName(path),
                            name,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                ?? Path.Combine(root, name);
        }
        return root;
    }

    private void RemoveEntry(string directory, string caption)
    {
        string path = Path.Combine(directory, "dune_folder.txt");
        if (!fileSystem.FileExists(path))
        {
            return;
        }
        string? content = RemoveCaption(
            fileSystem.ReadText(path),
            caption
        );
        if (content is null)
        {
            return;
        }
        if (
            content.Contains("item.0.", StringComparison.Ordinal)
        )
        {
            fileSystem.WriteBytes(
                path,
                LegacyDuneEncoding.Encode(
                    content
                        .TrimEnd('\r', '\n')
                        .Replace(
                            "\r\n",
                            "|",
                            StringComparison.Ordinal
                        ),
                    true
                )
            );
        }
        else
        {
            fileSystem.DeleteDirectory(directory);
        }
    }

    private static string? RemoveCaption(
        string content,
        string caption
    )
    {
        // RemoveIndexItem ignore la première ligne réservée à l'en-tête.
        content = content[(content.IndexOf('\n') + 1)..];
        string pattern =
            @"item\.\d{1,5}\.caption="
            + EscapeCaption(caption)
            + "\r\n";
        Match match = Regex.Match(content, pattern);
        if (!match.Success)
        {
            return null;
        }
        int removed = int.Parse(
            match.Value.Split('.')[1],
            CultureInfo.InvariantCulture
        );
        content = Regex.Replace(
            content,
            @"item\."
                + removed.ToString(CultureInfo.InvariantCulture)
                + @"\..+",
            ""
        );
        content = Regex.Replace(
            content,
            @"item\.(\d{1,5})\.",
            match => Renumber(match, removed)
        );
        return content;
    }

    private static string Renumber(Match match, int removed)
    {
        int index = int.Parse(
            match.Groups[1].Value,
            CultureInfo.InvariantCulture
        );
        return index > removed
            ? "item."
                + (index - 1).ToString(
                    CultureInfo.InvariantCulture
                )
                + "."
            : match.Value;
    }

    private static string EscapeCaption(string caption)
    {
        // Les points restent des motifs, conformément à RemoveIndexItem de yaDIS2.
        foreach (
            char character in new[]
            {
                '(',
                ')',
                '[',
                ']',
                '{',
                '}',
                '$',
                '-',
                '+',
            }
        )
        {
            caption = caption.Replace(
                character.ToString(),
                "\\" + character,
                StringComparison.Ordinal
            );
        }
        return caption;
    }
}
