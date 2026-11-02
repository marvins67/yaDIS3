using Avalonia.Logging;

namespace YaDIS3.Desktop.Tests;

internal sealed class BindingLog : ILogSink
{
    public List<string> Errors { get; } = [];

    public bool IsEnabled(LogEventLevel level, string area)
    {
        return area == LogArea.Binding
            && level >= LogEventLevel.Warning;
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate
    )
    {
        if (IsEnabled(level, area))
        {
            Errors.Add(messageTemplate);
        }
    }

    public void Log(
        LogEventLevel level,
        string area,
        object? source,
        string messageTemplate,
        params object?[] propertyValues
    )
    {
        Log(
            level,
            area,
            source,
            messageTemplate
                + " | "
                + string.Join(
                    " | ",
                    propertyValues.Select(value =>
                        value?.ToString()
                    )
                )
        );
    }
}
