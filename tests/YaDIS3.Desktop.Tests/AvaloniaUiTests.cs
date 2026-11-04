using Xunit;

namespace YaDIS3.Desktop.Tests;

// Les plateformes et le dispatcher Avalonia sont partagés ;
// ne pas initialiser deux applications sur des threads concurrents.
[CollectionDefinition("Avalonia UI", DisableParallelization = true)]
public sealed class AvaloniaUiTests;
