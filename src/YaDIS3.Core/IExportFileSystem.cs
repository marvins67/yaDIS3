namespace YaDIS3.Core;

/// <summary>Rend substituable le stockage des fichiers générés par l'export.</summary>
public interface IExportFileSystem
{
    /// <summary>Indique si le fichier source est disponible.</summary>
    bool FileExists(string path);

    /// <summary>Indique si un dossier existe.</summary>
    bool DirectoryExists(string path);

    /// <summary>Crée le dossier de destination.</summary>
    void CreateDirectory(string path);

    /// <summary>Lit une configuration de workspace en UTF-8.</summary>
    string ReadText(string path);

    /// <summary>Écrit les octets exacts d'un fichier Dune.</summary>
    void WriteBytes(string path, byte[] bytes);

    /// <summary>Copie une icône déjà rendue.</summary>
    void CopyFile(
        string source,
        string destination,
        bool overwrite
    );

    /// <summary>Énumère les dossiers directs correspondant au motif.</summary>
    IEnumerable<string> EnumerateDirectories(
        string path,
        string pattern = "*"
    );
}
