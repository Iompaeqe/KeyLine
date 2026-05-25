using System.Windows;

namespace MacroSpammer.UI.Timeline;

public sealed class TimelineVisualItem
{
    public required UIElement Element { get; init; }
    public required double Left { get; set; }
    public required Size Size { get; init; }
    public object? AnimationKey { get; init; }

    public double Width => Size.Width;
    public double CenterX => Left + (Size.Width / 2.0);
}
