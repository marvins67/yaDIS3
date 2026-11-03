namespace YaDIS3.Core;

/// <summary>
/// Rend substituable le stockage des fichiers générés par
/// l'export.
/// </summary>
public interface IExportFileSystem
{
    /// <summary>
    /// Indique si le fichier source est disponible.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Indique si un dossier existe.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Crée le dossier de destination.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Lit une configuration de workspace en UTF-8.
    /// </summary>
    string ReadText(string path);

    /// <summary>
    /// Lit les octets exacts d'un document à restaurer.
    /// </summary>
    byte[] ReadBytes(string path);

    /// <summary>
    /// Supprime un fichier lors d'une restauration.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Remplace atomiquement un fichier de configuration validé.
    /// </summary>
    void MoveFile(
        string source,
        string destination,
        bool overwrite
    );

    /// <summary>
    /// Renomme un dossier lors de la copie du workspace.
    /// </summary>
    void MoveDirectory(string source, string destination);

    /// <summary>
    /// Lit les attributs des dossiers sources.
    /// </summary>
    FileAttributes GetAttributes(string path);

    /// <summary>
    /// Écrit les octets exacts d'un fichier Dune.
    /// </summary>
    void WriteBytes(string path, byte[] bytes);

    /// <summary>
    /// Copie une icône déjà rendue.
    /// </summary>
    void CopyFile(
        string source,
        string destination,
        bool overwrite
    );

    /// <summary>
    /// Supprime une catégorie devenue vide et ses illustrations.
    /// </summary>
    void DeleteDirectory(string path);

    /// <summary>
    /// Énumère les fichiers directs d'un dossier.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string path);

    /// <summary>
    /// Énumère les dossiers directs correspondant au motif.
    /// </summary>
    IEnumerable<string> EnumerateDirectories(
        string path,
        string pattern = "*"
    );
}
