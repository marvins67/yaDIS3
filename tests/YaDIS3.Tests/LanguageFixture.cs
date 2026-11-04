using System.Text;

namespace YaDIS3.Tests;

internal sealed class LanguageFixture : IDisposable
{
    internal string Directory { get; } =
        Path.Combine(
            Path.GetTempPath(),
            "yadis3-languages-" + Guid.NewGuid()
        );

    internal LanguageFixture()
    {
        System.IO.Directory.CreateDirectory(Directory);
        Write("fr.xml", "fr", "Français", "Configurer");
        Write("en.xml", "en", "English", "Setup");
    }

    internal void Write(
        string file,
        string code,
        string name,
        string label
    )
    {
        File.WriteAllText(
            Path.Combine(Directory, file),
            $"<strings><string id='0'>{code}</string><string id='1'>{name}</string><string id='10'>{label}</string></strings>",
            Encoding.UTF8
        );
    }

    public void Dispose() =>
        System.IO.Directory.Delete(Directory, true);
}
