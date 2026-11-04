using YaDIS3.Desktop.Localization;

namespace YaDIS3.Desktop.Tests;

internal sealed class RestartRecorder : IApplicationRestart
{
    public int Count { get; private set; }

    public void Restart() => Count++;
}
