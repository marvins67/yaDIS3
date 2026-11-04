using System.Xml.Linq;
using YaDIS3.Core;

namespace YaDIS3.Tests;

internal sealed class DurationReader : IVideoInformationReader
{
    public int Reads { get; private set; }

    public Task<XElement> ReadAsync(string file, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(new XElement("mediainfo", new XElement("duration", "7200000")));
    }
}
