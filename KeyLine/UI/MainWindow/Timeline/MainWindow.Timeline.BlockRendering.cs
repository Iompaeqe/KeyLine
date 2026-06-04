using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Config;
using KeyLine.UI.Nodes;
using KeyLine.UI.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    private void AddBlockBackgrounds(
        Canvas canvas,
        MacroTimeline timeline,
        IReadOnlyList<TimelineVisualItem> visualItems,
        bool isFirstRow)
    {
        var itemsByNode = visualItems
            .Where(item => item.Node != null)
            .ToDictionary(item => item.Node!, item => item);

        if (itemsByNode.Count == 0)
            return;

        var ranges = TimelineBlockService.BuildBlockRanges(timeline.Nodes)
            .Where(range => itemsByNode.ContainsKey(range.Start) && itemsByNode.ContainsKey(range.End))
            .OrderBy(range => range.StartIndex)
            .ToList();

        var rangesWithDepth = ranges
            .Select(range => new
            {
                Range = range,
                Depth = ranges.Count(other =>
                    other.StartIndex < range.StartIndex &&
                    other.EndIndex > range.EndIndex)
            })
            .ToList();

        var maxDepth = rangesWithDepth.Count == 0
            ? 0
            : rangesWithDepth.Max(item => item.Depth);

        const double naturalLabelStep = 17;

        var availableUpwardLabelBand = Math.Max(
            0,
            (isFirstRow ? TimelineHeaderTopExtra : TimelineRowGap) - 4);

        availableUpwardLabelBand = Math.Min(availableUpwardLabelBand, 42);

        var labelStep = maxDepth <= 0
            ? 0
            : Math.Min(naturalLabelStep, availableUpwardLabelBand / maxDepth);

        foreach (var item in rangesWithDepth)
        {
            var range = item.Range;
            var depth = item.Depth;

            var startItem = itemsByNode[range.Start];
            var endItem = itemsByNode[range.End];

            var left = startItem.Left - 4;
            var right = endItem.Left + endItem.Width + 4;

            var labelSlot = maxDepth - depth;
            var labelTop = -(labelSlot * labelStep);

            var top = labelTop + 7;
            var bottom = TimelineRowHeight - 4;
            var height = Math.Max(12, bottom - top);

            var background = new Border
            {
                Width = Math.Max(1, right - left),
                Height = height,
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 168, 85, 247)),
                Background = new SolidColorBrush(Color.FromArgb(34, 86, 66, 120)),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(background, left);
            Canvas.SetTop(background, top);
            Panel.SetZIndex(background, -30 - depth);
            canvas.Children.Add(background);

            var startLabel = CreateBlockLabel(
                timeline,
                range.Start,
                NodeDisplayFormatter.GetBlockTimelineLabel(range.Start),
                horizontalPadding: 7);
            Canvas.SetLeft(startLabel, left + 12);
            Canvas.SetTop(startLabel, labelTop);
            Panel.SetZIndex(startLabel, 20 + depth);
            canvas.Children.Add(startLabel);

            var endLabel = CreateBlockLabel(
                timeline,
                range.End,
                "End",
                horizontalPadding: 6);
            endLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var endLabelLeft = right - endLabel.DesiredSize.Width - 12;
            Canvas.SetLeft(endLabel, Math.Max(left + 12, endLabelLeft));
            Canvas.SetTop(endLabel, top + height - 10);
            Panel.SetZIndex(endLabel, 20 + depth);
            canvas.Children.Add(endLabel);
        }
    }

    private Border CreateBlockLabel(
        MacroTimeline timeline,
        MacroNode node,
        string text,
        double horizontalPadding)
    {
        var isSelected = IsStepSelected(timeline, node);

        var label = new Border
        {
            Padding = new Thickness(horizontalPadding, 1, horizontalPadding, 1),
            Margin = new Thickness(-10, -5, -10, -3),
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(isSelected
                ? Color.FromRgb(76, 29, 149)
                : Color.FromRgb(15, 20, 29)),
            BorderBrush = new SolidColorBrush(isSelected
                ? Color.FromRgb(226, 232, 240)
                : Color.FromArgb(150, 168, 85, 247)),
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            Cursor = Cursors.Hand,
            IsHitTestVisible = true,
            Tag = node,
            ToolTip = "Click to select block. Double-click to inspect.",
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Black,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(isSelected
                    ? Color.FromRgb(255, 255, 255)
                    : Color.FromRgb(233, 213, 255)),
                VerticalAlignment = VerticalAlignment.Center,

                // Important: let the Border receive the click, not the TextBlock.
                IsHitTestVisible = false
            }
        };

        AttachBlockLabelMouseHandlers(label, timeline, node);

        return label;
    }

    private static Border? CreateTimelineConnector(IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count <= 1)
            return null;

        var firstCenterX = visualItems[0].CenterX;
        var lastCenterX = visualItems[^1].CenterX;

        var connector = new Border
        {
            Width = Math.Max(0, lastCenterX - firstCenterX),
            Height = TimelineConnectorThickness,
            CornerRadius = new CornerRadius(TimelineConnectorThickness / 2.0),
            Background = new SolidColorBrush(Color.FromRgb(31, 48, 66)),
            Opacity = 0.85,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(connector, firstCenterX);
        Canvas.SetTop(
            connector,
            TimelineLayoutCalculator.GetConnectorTop(TimelineConnectorY, TimelineConnectorThickness));
        Panel.SetZIndex(connector, -10);

        return connector;
    }

    private void RegisterTimelineRowState(MacroTimeline timeline, Canvas canvas, Border? connector,
        IReadOnlyList<TimelineVisualItem> visualItems)
    {
        if (visualItems.Count == 0)
            return;

        var addItem = visualItems[^1];

        _timelineRowRenderStates[timeline] = new TimelineRowRenderState
        {
            Timeline = timeline,
            Canvas = canvas,
            Connector = connector,
            AddButton = addItem.Element as FrameworkElement,
            NextLeft = addItem.Left,
            FirstCenterX = visualItems[0].CenterX,
            LastCenterX = visualItems[^1].CenterX,
            RowWidth = addItem.Left + addItem.Width + TimelineRightPadding,
            VisualItemCount = visualItems.Count,
            VisualItems = visualItems.ToList(),
            VisualItemsByAnimationKey = BuildVisualItemLookup(visualItems)
        };
    }

    private static Dictionary<object, TimelineVisualItem> BuildVisualItemLookup(
        IReadOnlyList<TimelineVisualItem> visualItems)
    {
        var lookup = new Dictionary<object, TimelineVisualItem>();

        foreach (var item in visualItems)
        {
            if (item.AnimationKey != null)
                lookup[item.AnimationKey] = item;
        }

        return lookup;
    }
}
