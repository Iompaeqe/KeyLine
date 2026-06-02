namespace KeyLine.Services.Timeline;

public static class DelayFormatter
{
    public static string Format(int milliseconds)
    {
        if (milliseconds < 1000)
            return milliseconds.ToString();

        var totalSeconds = milliseconds / 1000.0;

        if (totalSeconds < 60)
            return FormatUnit(totalSeconds, "s");

        var totalMinutes = totalSeconds / 60.0;

        if (totalMinutes < 60)
            return FormatUnit(totalMinutes, "m");

        var totalHours = totalMinutes / 60.0;

        return FormatUnit(totalHours, "h");
    }

    public static (string Value, string Unit) Split(int milliseconds)
    {
        var text = Format(milliseconds);

        if (text.EndsWith("s"))
            return (text[..^1], "s");

        if (text.EndsWith("m"))
            return (text[..^1], "m");

        if (text.EndsWith("h"))
            return (text[..^1], "h");

        return (text, "ms");
    }

    private static string FormatUnit(double value, string unit)
    {
        return value % 1 == 0
            ? $"{value:0}{unit}"
            : $"{value:0.##}{unit}";
    }
}