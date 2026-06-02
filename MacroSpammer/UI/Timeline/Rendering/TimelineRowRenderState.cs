using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;
using System.Collections.Generic;

namespace MacroSpammer.UI.Timeline;

public sealed class TimelineRowRenderState
{
    public required MacroTimeline Timeline { get; init; }
    public required Canvas Canvas { get; init; }
    public Border? Connector { get; set; }
    public FrameworkElement? AddButton { get; set; }
    public double NextLeft { get; set; }
    public double FirstCenterX { get; set; }
    public double LastCenterX { get; set; }
    public double RowWidth { get; set; }
    public int VisualItemCount { get; set; }
    public List<TimelineVisualItem> VisualItems { get; set; } = new();
    public Dictionary<object, TimelineVisualItem> VisualItemsByAnimationKey { get; set; } = new();
}
