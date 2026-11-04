using System.Configuration;

namespace YaDIS3.Core;

/// <summary>Demande au fournisseur .NET le chemin de user.config.</summary>
public static class UserConfigurationLocation
{
    /// <summary>Chemin propre au compte et à l'application courante.</summary>
    public static string Resolve() =>
        ConfigurationManager
            .OpenExeConfiguration(
                ConfigurationUserLevel.PerUserRoamingAndLocal
            )
            .FilePath;
}
