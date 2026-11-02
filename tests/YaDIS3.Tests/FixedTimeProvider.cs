namespace YaDIS3.Tests;

internal sealed class FixedTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() =>
        new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone =>
        TimeZoneInfo.Utc;
}
