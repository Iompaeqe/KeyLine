using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Nodes;

namespace KeyLine.UI.Timeline;

public sealed partial class TimelineRenderer
{
    private List<TimelineRowVisualModel> BuildTimelineRowVisualModels()
    {
        var displayTimelines = GetDisplayTimelines();
        var rows = new List<TimelineRowVisualModel>(displayTimelines.Count);

        for (var i = 0; i < displayTimelines.Count; i++)
        {
            var timeline = displayTimelines[i];
            var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
                _context.GetRenderRawSteps(timeline).ToList(),
                timeline.UseStandardDelay,
                timeline.ShowKeyUpDown);

            rows.Add(new TimelineRowVisualModel
            {
                Timeline = timeline,
                VisualItems = BuildTimelineVisualItems(timeline, visibleSteps),
                IsFirstRow = i == 0,
                IsLastRow = i == displayTimelines.Count - 1
            });
        }

        return rows;
    }

    private double GetTimelineCanvasWidth(IEnumerable<TimelineRowVisualModel> rows)
    {
        var maxContentWidth = rows.Aggregate(
            TimelineFirstItemLeft + TimelineRightPadding,
            (maxWidth, row) => Math.Max(maxWidth, row.RowWidth));

        return Math.Max(GetMinimumTimelineCanvasWidth(), maxContentWidth);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> visibleSteps,
        double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        return CreateTimelineRow(
            timeline,
            BuildTimelineVisualItems(timeline, visibleSteps),
            canvasWidth,
            isFirstRow,
            isLastRow);
    }

    private UIElement CreateTimelineRow(MacroTimeline timeline, IReadOnlyList<TimelineVisualItem> visualItems,
        double canvasWidth, bool isFirstRow, bool isLastRow)
    {
        var collapsed = IsEffectivelyCollapsed(timeline);

        var row = new Grid
        {
            Height = GetDisplayRowHeight(timeline),
            Margin = new Thickness(
                0,
                0,
                0,
                TimelineLayoutCalculator.GetRowBottomMargin(isLastRow, GetDisplayRowGap(timeline))),
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        // The node canvas is always built at full height; collapsing just hides it (and shows the
        // summary overlay), so expand/collapse toggles visibility instead of rebuilding node visuals.
        var canvas = new Canvas
        {
            Width = canvasWidth,
            Height = TimelineRowHeight,
            Background = Brushes.Transparent,
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible
        };

        row.Children.Add(canvas);

        AddBlockBackgrounds(canvas, timeline, visualItems, isFirstRow);

        var connector = CreateTimelineConnector(visualItems);
        if (connector != null)
            canvas.Children.Add(connector);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        var summary = CreateCollapsedRowSummary(timeline);
        summary.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        row.Children.Add(summary);

        RegisterTimelineRowState(timeline, canvas, connector, visualItems, row, summary);

        return row;
    }

    // Faint "N nodes" overlay shown in place of the node canvas while a timeline is collapsed.
    private TextBlock CreateCollapsedRowSummary(MacroTimeline timeline)
    {
        var summary = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(94, 113, 137)),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(TimelineFirstItemLeft, 0, 0, 0),
            IsHitTestVisible = false
        };

        UpdateCollapsedSummaryText(summary, timeline);
        return summary;
    }

    // Refreshes the collapsed summary's node count (used when a timeline is collapsed after edits).
    private static void UpdateCollapsedSummaryText(FrameworkElement summary, MacroTimeline timeline)
    {
        if (summary is not TextBlock text)
            return;

        var nodeCount = timeline.Nodes.Count(node => !node.IsSyntheticDisplayNode);
        text.Text = nodeCount == 1 ? "1 node" : $"{nodeCount} nodes";
    }

    private List<TimelineVisualItem> BuildTimelineVisualItems(MacroTimeline timeline,
        IReadOnlyList<MacroNode> visibleSteps)
    {
        var visualItems = new List<TimelineVisualItem>();

        // Node items are built even for collapsed rows: the row builder hides the canvas rather
        // than dropping the visuals, so a later expand can reuse them without a rebuild.
        var currentLeft = TimelineFirstItemLeft;

        var isDraggingThisTimeline =
            _drag.IsDraggingNode &&
            ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
            _drag.DraggedNode != null;

        var placeholderCount = 0;
        var previewSlots = isDraggingThisTimeline
            ? _context.GetRenderPreviewSlots(timeline)
            : Array.Empty<NodePreviewSlot>();

        if (isDraggingThisTimeline && previewSlots.Count > 0)
        {
            foreach (var slot in previewSlots)
            {
                if (slot.IsDraggedSlot)
                {
                    AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode, slot.Width);
                    placeholderCount++;
                    continue;
                }

                if (ShouldShowBlockAddButton(timeline, slot.DisplayNode))
                    AddBlockAddVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode);

                AddNodeVisualItem(visualItems, ref currentLeft, timeline, slot.DisplayNode);
            }
        }
        else
        {
            foreach (var step in visibleSteps)
            {
                if (ShouldShowBlockAddButton(timeline, step))
                    AddBlockAddVisualItem(visualItems, ref currentLeft, timeline, step);

                AddNodeVisualItem(visualItems, ref currentLeft, timeline, step);
            }
        }

        if (isDraggingThisTimeline && placeholderCount == 0)
            AddPlaceholderVisualItem(visualItems, ref currentLeft, timeline, _drag.DraggedNode!);

        var addBlock = CreateAddNode(timeline);
        var addSize = MeasureTimelineItem(addBlock);

        visualItems.Add(new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = (timeline, "add")
        });

        return visualItems;
    }

    private void AddNodeVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode step)
    {
        visualItems.Add(BuildNodeVisualItem(timeline, step, ref currentLeft));
    }

    // Creates the control for a node, measures it, and packages it as a positioned visual item,
    // advancing the running left edge by the item width + gap. Shared by the full row build and
    // the append-only recording path so node-item construction lives in exactly one place.
    private TimelineVisualItem BuildNodeVisualItem(MacroTimeline timeline, MacroNode step, ref double currentLeft)
    {
        var block = CreateNode(timeline, step);

        if (block is FrameworkElement element)
            element.Tag = step;

        var size = MeasureTimelineItem(block);

        var item = new TimelineVisualItem
        {
            Node = step,
            Element = block,
            Left = currentLeft,
            Size = size,
            AnimationKey = GetTimelineAnimationKey(timeline, step)
        };

        currentLeft += size.Width + TimelineItemGap;
        return item;
    }

    private void AddBlockAddVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode rawInsertAnchor)
    {
        var addBlock = CreateAddNode(timeline, rawInsertAnchor);
        var addSize = MeasureTimelineItem(addBlock);

        visualItems.Add(new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = GetBlockAddAnimationKey(timeline, rawInsertAnchor)
        });

        currentLeft += addSize.Width + TimelineItemGap;
    }

    private static bool ShouldShowBlockAddButton(MacroTimeline timeline, MacroNode node)
    {
        return TimelineBlockService.IsBlockEnd(node) &&
               TimelineBlockService.TryGetBlockRange(timeline, node, out _);
    }

    private object GetDropPlaceholderAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        return (timeline, "drop-placeholder", GetTimelineAnimationKey(timeline, node));
    }

    private object GetBlockAddAnimationKey(MacroTimeline timeline, MacroNode rawInsertAnchor)
    {
        return (timeline, "block-add", rawInsertAnchor);
    }

    public void RemoveDropPlaceholderAnimationKeys(MacroTimeline timeline)
    {
        var keysToRemove = _timelineVisualPositions.Keys
            .Where(key => IsDropPlaceholderAnimationKeyForTimeline(key, timeline))
            .ToList();

        foreach (var key in keysToRemove)
            _timelineVisualPositions.Remove(key);
    }

    private void AddPlaceholderVisualItem(
        List<TimelineVisualItem> visualItems,
        ref double currentLeft,
        MacroTimeline timeline,
        MacroNode draggedNode,
        double? widthOverride = null)
    {
        var draggedBlock = CreateNode(timeline, draggedNode);
        var draggedSize = MeasureTimelineItem(draggedBlock);
        if (widthOverride.HasValue)
            draggedSize = new Size(widthOverride.Value, draggedSize.Height);

        var placeholder = new Border
        {
            Width = draggedSize.Width,
            Height = draggedSize.Height,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1.5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(150, 96, 165, 250)),
            Background = new SolidColorBrush(Color.FromArgb(30, 96, 165, 250)),
            Opacity = 1.0,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.96, 0.96)
        };

        var shouldAnimatePlaceholder = IsTimelineItemNearViewport(currentLeft, currentLeft, draggedSize.Width);
        if (shouldAnimatePlaceholder && placeholder.RenderTransform is ScaleTransform scale)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(90));
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            var animation = new DoubleAnimation(1.0, duration) { EasingFunction = easing };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        else if (placeholder.RenderTransform is ScaleTransform restingScale)
        {
            restingScale.ScaleX = 1.0;
            restingScale.ScaleY = 1.0;
        }

        visualItems.Add(new TimelineVisualItem
        {
            Element = placeholder,
            Left = currentLeft,
            Size = draggedSize,
            AnimationKey = GetDropPlaceholderAnimationKey(timeline, draggedNode)
        });

        currentLeft += draggedSize.Width + TimelineItemGap;
    }

    public object GetTimelineAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        var rawItems = TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, node);

        if (rawItems.Count == 0)
            return node;

        return rawItems[0];
    }

    public double GetCachedNodePreviewWidth(MacroTimeline timeline, MacroNode node)
    {
        var animationKey = GetTimelineAnimationKey(timeline, node);
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state) &&
            state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
        {
            return GetStableNodePreviewWidth(node, item.Width);
        }

        return GetStableNodePreviewWidth(node, MeasureTimelineItem(CreateNode(timeline, node)).Width);
    }

    public double GetLeadingNodePreviewWidth(MacroTimeline timeline, MacroNode node)
    {
        if (!ShouldShowBlockAddButton(timeline, node))
            return 0;

        return Math.Max(0, MeasureTimelineItem(CreateAddNode(timeline, node)).Width);
    }

    private static double GetStableNodePreviewWidth(MacroNode node, double measuredWidth)
    {
        var minimumWidth = TimelineBlockService.IsBlockBoundary(node)
            ? BlockNode.BoundaryNodeWidth
            : 1;

        return Math.Max(minimumWidth, measuredWidth);
    }
}
