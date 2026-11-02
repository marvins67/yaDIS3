using Xunit;

// Les rendus Skia de plusieurs catalogues consomment beaucoup de mémoire native.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
