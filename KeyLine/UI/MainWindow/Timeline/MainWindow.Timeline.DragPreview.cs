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
    private void RefreshTimelineDragPreview()
    {
        if (TimelineRowsPanel == null)
            return;

        if (!_drag.IsDraggingNode || _drag.DraggedNodeTimeline == null)
        {
            RefreshTimeline();
            return;
        }

        var timeline = _drag.DraggedNodeTimeline;

        // Fast path: if we already have a render state for this row, just update positions
        if (_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            // The problem with the fast path is that the "placeholder" is only present in state.VisualItems
            // IF the row was already rendered in "dragging mode".
            // If it's the FIRST move after BeginStepDrag, state.VisualItems doesn't have the placeholder.
            // In that case, we MUST do a full row refresh once to get the placeholder element created.

            var hasPlaceholder = state.VisualItems.Any(item => IsDropPlaceholderAnimationKey(item.AnimationKey));
            if (!hasPlaceholder)
            {
                RefreshTimelineRow(timeline);
                return;
            }

            if (state.VisualItems.Any(item => IsBlockAddAnimationKey(item.AnimationKey)))
            {
                RefreshTimelineRow(timeline);
                return;
            }

            var previewSlots = GetTimelineRenderPreviewSlots(timeline);

            // Re-calculate positions for all items including the placeholder.
            var currentLeft = TimelineFirstItemLeft;
            var placeholderAdded = false;

            var orderedVisualItems = new List<TimelineVisualItem>(state.VisualItems.Count);

            foreach (var slot in previewSlots)
            {
                if (slot.IsDraggedSlot)
                {
                    var placeholderKey = GetDropPlaceholderAnimationKey(timeline, slot.DisplayNode);
                    if (!UpdateOrMoveVisualItem(
                            state,
                            orderedVisualItems,
                            ref currentLeft,
                            placeholderKey))
                        return;
                    placeholderAdded = true;
                    continue;
                }

                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref currentLeft,
                        GetTimelineAnimationKey(timeline, slot.DisplayNode)))
                    return;
            }

            if (!placeholderAdded)
            {
                var placeholderKey = GetDropPlaceholderAnimationKey(timeline, _drag.DraggedNode!);
                if (!UpdateOrMoveVisualItem(
                        state,
                        orderedVisualItems,
                        ref currentLeft,
                        placeholderKey))
                    return;
            }

            // Finally, the Add button
            if (!UpdateOrMoveVisualItem(
                    state,
                    orderedVisualItems,
                    ref currentLeft,
                    (timeline, "add")))
                return;

            state.VisualItems = orderedVisualItems;
            state.VisualItemsByAnimationKey = BuildVisualItemLookup(orderedVisualItems);
            state.VisualItemCount = orderedVisualItems.Count;
            state.NextLeft = orderedVisualItems[^1].Left;
            state.RowWidth = orderedVisualItems[^1].Left + orderedVisualItems[^1].Width + TimelineRightPadding;

            if (state.RowWidth > state.Canvas.Width + 1)
                EnsureTimelineCanvasWidthForAllRows(state.RowWidth);

            UpdateRowConnector(state, animate: false);
            return;
        }

        RefreshTimeline();
    }

    private bool UpdateOrMoveVisualItem(
        TimelineRowRenderState state,
        List<TimelineVisualItem> orderedVisualItems,
        ref double currentLeft,
        object animationKey)
    {
        if (!state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item))
        {
            RefreshTimelineRow(state.Timeline);
            return false;
        }

        if (!orderedVisualItems.Contains(item))
            orderedVisualItems.Add(item);

        var previousLeft = Canvas.GetLeft(item.Element);
        if (Math.Abs(previousLeft - currentLeft) > 0.1)
        {
            // Animate smooth movement if it moved significantly
            var deltaX = previousLeft - currentLeft;
            if (Math.Abs(deltaX) > 0.5 && IsTimelineItemNearViewport(previousLeft, currentLeft, item.Width))
                TimelineAnimationService.AnimateRenderOffsetToRest(item.Element, deltaX, 0, animateY: false);

            Canvas.SetLeft(item.Element, currentLeft);
        }

        item.Left = currentLeft;
        currentLeft += item.Width + TimelineItemGap;

        // Update the cached position for future full refreshes
        _timelineVisualPositions[animationKey] = new Point(item.Left,
            TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, item.Size.Height));
        return true;
    }

    private double GetExistingTimelineCanvasWidth(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < TimelineRowsPanel.Children.Count)
        {
            if (TimelineRowsPanel.Children[rowIndex] is Grid row)
            {
                foreach (var canvas in row.Children.OfType<Canvas>())
                {
                    if (canvas.Width > 0)
                        return canvas.Width;
                }
            }
        }

        return GetMinimumTimelineCanvasWidth();
    }
}
