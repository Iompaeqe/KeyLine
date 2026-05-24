using System.Windows.Media;

namespace KeySpammer.UI.Config;

public static class UiBrushes
{
    private static readonly Dictionary<Color, SolidColorBrush> BrushCache = new();

    public static SolidColorBrush Get(Color color)
    {
        if (BrushCache.TryGetValue(color, out var cached))
            return cached;

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        BrushCache[color] = brush;
        return brush;
    }
}
