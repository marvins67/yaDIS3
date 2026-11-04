namespace YaDIS3.Core;

/// <summary>
/// Traduit un montage SMB Finder en UNC avant d'appliquer GetDunePath.
/// Les identifiants utilisateur ne figurent pas dans un chemin UNC Windows.
/// </summary>
public static class MacSmbMounts
{
    /// <summary>Retrouve le montage parent le plus proche du dossier choisi.</summary>
    public static string NetworkPath(
        string folder,
        string mountOutput
    )
    {
        string result = "";
        int longestRoot = 0;
        foreach (string line in mountOutput.Split('\n'))
        {
            int delimiter = line.IndexOf(
                " on ",
                StringComparison.Ordinal
            );
            int flags = line.LastIndexOf(
                " (smbfs",
                StringComparison.Ordinal
            );
            if (delimiter < 0 || flags < delimiter)
            {
                continue;
            }
            string root = line[(delimiter + 4)..flags];
            if (
                root.Length < longestRoot
                || !IsWithin(folder, root)
            )
            {
                continue;
            }
            Uri share = new("smb:" + line[..delimiter]);
            string relative = folder[root.Length..];
            result =
                @"\\"
                + share.Host
                + Uri.UnescapeDataString(share.AbsolutePath)
                    .Replace('/', '\\')
                + relative.Replace('/', '\\');
            longestRoot = root.Length;
        }
        return result;
    }

    private static bool IsWithin(string folder, string root) =>
        folder == root
        || folder.StartsWith(
            root + "/",
            StringComparison.Ordinal
        );
}
