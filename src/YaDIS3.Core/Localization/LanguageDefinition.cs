namespace YaDIS3.Core;

/// <summary>Code et nom fournis par les libellés 0 et 1 d'un XML.</summary>
public sealed record LanguageDefinition(string Code, string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
