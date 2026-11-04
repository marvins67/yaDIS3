using System.Globalization;
using System.Runtime.CompilerServices;

namespace YaDIS3.Desktop.Tests;

internal static class TestLanguageCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Les captures et assertions historiques de cette suite sont françaises.
        CultureInfo.DefaultThreadCurrentUICulture = new("fr-FR");
        CultureInfo.CurrentUICulture = new("fr-FR");
    }
}
