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
    private List<TimelineRowVisualModel> BuildTimelineRowVisualModels()
    {
        var displayTimelines = GetDisplayTimelines();
        var rows = new List<TimelineRowVisualModel>(displayTimelines.Count);

        for (var i = 0; i < displayTimelines.Count; i++)
        {
            var timeline = displayTimelines[i];
            var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
                GetTimelineRenderRawSteps(timeline).ToList(),
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
        var row = new Grid
        {
            Height = TimelineRowHeight,
            Margin = new Thickness(
                0,
                0,
                0,
                TimelineLayoutCalculator.GetRowBottomMargin(isLastRow, TimelineRowGap)),
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        var canvas = new Canvas
        {
            Width = canvasWidth,
            Height = TimelineRowHeight,
            Background = Brushes.Transparent,
            Tag = timeline,
            VerticalAlignment = VerticalAlignment.Top
        };

        row.Children.Add(canvas);

        AddBlockBackgrounds(canvas, timeline, visualItems, isFirstRow);

        var connector = CreateTimelineConnector(visualItems);
        if (connector != null)
            canvas.Children.Add(connector);

        foreach (var item in visualItems)
            AddTimelineItem(canvas, item);

        RegisterTimelineRowState(timeline, canvas, connector, visualItems);

        return row;
    }

    private List<TimelineVisualItem> BuildTimelineVisualItems(MacroTimeline timeline,
        IReadOnlyList<MacroNode> visibleSteps)
    {
        var visualItems = new List<TimelineVisualItem>();
        var currentLeft = TimelineFirstItemLeft;

        var isDraggingThisTimeline =
            _drag.IsDraggingNode &&
            ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
            _drag.DraggedNode != null;

        var placeholderCount = 0;
        var previewSlots = isDraggingThisTimeline
            ? GetTimelineRenderPreviewSlots(timeline)
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
        var block = CreateNode(timeline, step);

        if (block is FrameworkElement element)
            element.Tag = step;

        var size = MeasureTimelineItem(block);

        visualItems.Add(new TimelineVisualItem
        {
            Node = step,
            Element = block,
            Left = currentLeft,
            Size = size,
            AnimationKey = GetTimelineAnimationKey(timeline, step)
        });

        currentLeft += size.Width + TimelineItemGap;
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

    private void RemoveDropPlaceholderAnimationKeys(MacroTimeline timeline)
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

    private object GetTimelineAnimationKey(MacroTimeline timeline, MacroNode node)
    {
        var rawItems = TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, node);

        if (rawItems.Count == 0)
            return node;

        return rawItems[0];
    }

    private double GetCachedNodePreviewWidth(MacroTimeline timeline, MacroNode node)
    {
        var animationKey = GetTimelineAnimationKey(timeline, node);
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state) &&
            state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
        {
            return GetStableNodePreviewWidth(node, item.Width);
        }

        return GetStableNodePreviewWidth(node, MeasureTimelineItem(CreateNode(timeline, node)).Width);
    }

    private double GetLeadingNodePreviewWidth(MacroTimeline timeline, MacroNode node)
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
