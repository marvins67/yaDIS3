using System.Text;

namespace YaDIS3.Core;

/// <summary>Mémorise le dernier workspace ouvert sur ce poste.</summary>
public sealed class LastWorkspaceStore(
    string settingsDirectory,
    IExportFileSystem files
)
{
    private const string FileName = "last-workspace.txt";

    /// <summary>Relit le chemin de la bibliothèque précédente.</summary>
    public string? Load()
    {
        string file = Path.Combine(settingsDirectory, FileName);
        return files.FileExists(file)
            ? files.ReadText(file)
            : null;
    }

    /// <summary>Mémorise uniquement une ouverture réussie.</summary>
    public void Save(string workspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        files.CreateDirectory(settingsDirectory);
        string target = Path.Combine(
            settingsDirectory,
            FileName
        );
        string temporary =
            target + "." + Guid.NewGuid().ToString("N");
        try
        {
            files.WriteBytes(
                temporary,
                Encoding.UTF8.GetBytes(workspace)
            );
            files.MoveFile(temporary, target, overwrite: true);
        }
        finally
        {
            if (files.FileExists(temporary))
            {
                files.DeleteFile(temporary);
            }
        }
    }
}
