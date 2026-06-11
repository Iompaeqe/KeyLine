using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Nodes;

namespace KeyLine.UI.Timeline;

public sealed partial class TimelineRenderer
{
    public void RefreshTimelineRow(MacroTimeline timeline)
    {
        if (TimelineRowsPanel == null)
            return;

        var displayTimelines = GetDisplayTimelines();
        var rowIndex = displayTimelines.IndexOf(timeline);
        if (rowIndex < 0 || rowIndex >= TimelineRowsPanel.Children.Count)
        {
            RefreshTimeline();
            return;
        }

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _context.GetRenderRawSteps(timeline).ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var canvasWidth = GetExistingTimelineCanvasWidth(rowIndex);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            canvasWidth,
            rowIndex == 0,
            rowIndex == displayTimelines.Count - 1);

        TimelineRowsPanel.Children.RemoveAt(rowIndex);
        TimelineRowsPanel.Children.Insert(rowIndex, replacementRow);

        FitTimelineCanvasWidthToCurrentContent();
        Dispatcher.BeginInvoke(new Action(_context.UpdateScrollIndicator));
    }

    // Timeline-level refresh that also re-syncs header highlights. Editing one timeline's nodes
    // (add/delete/clear/edit) typically calls SelectTimeline first, so the active timeline — and
    // thus other rows' header highlight — may have changed even though only one row's nodes did.
    public void RefreshTimelineRowAndHeaders(MacroTimeline timeline)
    {
        RefreshTimelineRow(timeline);
        UpdateTimelineHeaderActiveStates();
    }

    // Collapse tier: toggle the existing row's container — hide/show the node canvas, swap in the
    // summary overlay, resize the row — and resize/re-skin the headers. No node visuals are rebuilt,
    // and unrelated timelines are untouched. Falls back to a single-row rebuild only if this row
    // has no reusable container yet.
    public void RefreshTimelineCollapse(MacroTimeline timeline)
    {
        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state) ||
            state.RowContainer == null || state.CollapsedSummary == null)
        {
            RefreshTimelineRow(timeline);
            RefreshTimelineHeaders();
            _context.UpdateWindowHeight();
            return;
        }

        var collapsed = IsEffectivelyCollapsed(timeline);

        state.Canvas.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        if (collapsed)
            UpdateCollapsedSummaryText(state.CollapsedSummary, timeline);
        state.CollapsedSummary.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;

        var displayTimelines = GetDisplayTimelines();
        var rowIndex = displayTimelines.IndexOf(timeline);
        var isLastRow = rowIndex == displayTimelines.Count - 1;

        state.RowContainer.Height = GetDisplayRowHeight(timeline);
        state.RowContainer.Margin = new Thickness(
            0, 0, 0, TimelineLayoutCalculator.GetRowBottomMargin(isLastRow, GetDisplayRowGap(timeline)));

        // Resize the matching header rows + re-skin headers (collapsed body/chevron), reusing the
        // existing header controls rather than rebuilding them.
        UpdateHeaderGridRowHeights();
        UpdateTimelineHeaderActiveStates();

        _context.UpdateWindowHeight();
    }

    // Node-level refresh: one node's value changed in place (same instance). Re-renders the
    // existing control without recreating it. If the edit changed the node's footprint, a
    // single-row rebuild reflows the row (and keeps block backgrounds aligned) — still far
    // cheaper than a full timeline teardown. Falls back to a row rebuild if the node has no
    // live visual (e.g. collapsed row, combo display).
    public void RefreshTimelineNode(MacroTimeline timeline, MacroNode node)
    {
        if (TimelineRowsPanel == null ||
            !_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            RefreshTimelineRow(timeline);
            return;
        }

        var animationKey = GetTimelineAnimationKey(timeline, node);
        if (!state.VisualItemsByAnimationKey.TryGetValue(animationKey, out var item) ||
            item.Element is not NodeBase nodeControl)
        {
            RefreshTimelineRow(timeline);
            return;
        }

        nodeControl.IsSelected = IsStepSelected(timeline, node);
        nodeControl.RefreshVisual();

        var newSize = MeasureTimelineItem(nodeControl);
        if (Math.Abs(newSize.Width - item.Width) > 0.5)
            RefreshTimelineRow(timeline);
    }

    public void UpdateSelectionVisuals(MacroTimeline? previousTimeline, MacroTimeline? currentTimeline)
    {
        UpdateSelectionVisualsForTimeline(previousTimeline);

        if (currentTimeline != null && !ReferenceEquals(currentTimeline, previousTimeline))
            UpdateSelectionVisualsForTimeline(currentTimeline);
    }

    private void UpdateSelectionVisualsForTimeline(MacroTimeline? timeline)
    {
        if (timeline == null)
            return;

        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state))
            return;

        foreach (var item in state.VisualItems)
        {
            if (item.Element is not NodeBase nodeControl || nodeControl.Node == null)
                continue;

            nodeControl.IsSelected = IsStepSelected(timeline, nodeControl.Node);
        }
    }

    public void AppendRecordedStepsToTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> addedRawSteps)
    {
        if (addedRawSteps.Count == 0)
            return;

        // Combo display mode can retroactively change the previous visible item,
        // so append-only rendering is unsafe there. Fall back to row-only rebuild.
        if (timeline.UseStandardDelay && !timeline.ShowKeyUpDown)
        {
            RefreshTimelineRow(timeline);
            return;
        }

        if (!_timelineRowRenderStates.TryGetValue(timeline, out var state))
        {
            RefreshTimelineRow(timeline);
            return;
        }

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            addedRawSteps,
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        if (visibleSteps.Count == 0)
            return;

        if (state.AddButton != null)
        {
            state.Canvas.Children.Remove(state.AddButton);
            state.VisualItems.RemoveAll(item => ReferenceEquals(item.Element, state.AddButton));
            state.VisualItemsByAnimationKey.Remove((timeline, "add"));
            state.VisualItemCount = Math.Max(0, state.VisualItemCount - 1);
        }

        var currentLeft = state.NextLeft;

        foreach (var step in visibleSteps)
        {
            var item = BuildNodeVisualItem(timeline, step, ref currentLeft);

            AddTimelineItem(state.Canvas, item);
            state.VisualItems.Add(item);
            if (item.AnimationKey != null)
                state.VisualItemsByAnimationKey[item.AnimationKey] = item;
            UpdateRowConnectorBounds(state, item);
        }

        var addBlock = CreateAddNode(timeline);
        var addSize = MeasureTimelineItem(addBlock);

        var addItem = new TimelineVisualItem
        {
            Element = addBlock,
            Left = currentLeft,
            Size = addSize,
            AnimationKey = (timeline, "add")
        };

        AddTimelineItem(state.Canvas, addItem);
        state.VisualItems.Add(addItem);
        state.VisualItemsByAnimationKey[(timeline, "add")] = addItem;
        UpdateRowConnectorBounds(state, addItem);

        state.AddButton = addBlock as FrameworkElement;
        state.NextLeft = currentLeft;

        var requiredWidth = currentLeft + addSize.Width + TimelineRightPadding;
        state.RowWidth = requiredWidth;
        EnsureTimelineCanvasWidthForAllRows(requiredWidth);

        UpdateRowConnector(state);

        Dispatcher.BeginInvoke(new Action(_context.UpdateScrollIndicator));
    }

    private void UpdateRowConnectorBounds(TimelineRowRenderState state, TimelineVisualItem item)
    {
        if (state.VisualItemCount == 0)
            state.FirstCenterX = item.CenterX;

        state.LastCenterX = item.CenterX;
        state.VisualItemCount++;
    }

    private void UpdateRowConnector(TimelineRowRenderState state, bool animate = true)
    {
        if (state.VisualItems.Count <= 1)
        {
            if (state.Connector != null)
            {
                state.Canvas.Children.Remove(state.Connector);
                state.Connector = null;
            }

            return;
        }

        var firstItem = state.VisualItems[0];
        var lastItem = state.VisualItems[^1];

        var firstCenterX = Canvas.GetLeft(firstItem.Element) + (firstItem.Width / 2.0);
        var lastCenterX = Canvas.GetLeft(lastItem.Element) + (lastItem.Width / 2.0);

        if (state.Connector == null)
        {
            state.Connector = CreateConnectorBar(firstCenterX, lastCenterX - firstCenterX);
            state.Canvas.Children.Insert(0, state.Connector);
        }

        // Animate connector smoothly if items moved
        var previousLeft = Canvas.GetLeft(state.Connector);
        var previousWidth = state.Connector.Width;
        var targetWidth = Math.Max(0, lastCenterX - firstCenterX);

        if (Math.Abs(previousLeft - firstCenterX) > 0.1 || Math.Abs(previousWidth - targetWidth) > 0.1)
        {
            Canvas.SetLeft(state.Connector, firstCenterX);
            state.Connector.Width = targetWidth;

            var deltaX = previousLeft - firstCenterX;
            var deltaWidth = previousWidth - targetWidth;

            if (animate &&
                (Math.Abs(deltaX) > 0.5 || Math.Abs(deltaWidth) > 0.5) &&
                IsTimelineItemNearViewport(previousLeft, firstCenterX, Math.Max(previousWidth, targetWidth)))
            {
                TimelineAnimationService.AnimateRenderOffsetToRest(state.Connector, deltaX, 0, animateY: false);

                var duration = new Duration(TimeSpan.FromMilliseconds(130));
                var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

                // Width animation is more expensive as it triggers layout, but it's needed for the connector
                var widthAnim = new DoubleAnimation(previousWidth, targetWidth, duration) { EasingFunction = easing };
                state.Connector.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }
        }
    }

    public void EnsureTimelineCanvasWidthForAllRows(double requiredWidth)
    {
        if (TimelineRowsPanel == null)
            return;

        var width = GetSharedTimelineCanvasWidth(requiredWidth);
        SetTimelineCanvasWidthForAllRows(width);
    }

    private void FitTimelineCanvasWidthToCurrentContent()
    {
        if (TimelineRowsPanel == null)
            return;

        SetTimelineCanvasWidthForAllRows(GetSharedTimelineCanvasWidth());
    }

    private void SetTimelineCanvasWidthForAllRows(double width)
    {
        TimelineRowsPanel.Width = width;

        foreach (var row in TimelineRowsPanel.Children.OfType<Grid>())
        {
            foreach (var canvas in row.Children.OfType<Canvas>())
                canvas.Width = width;
        }

        foreach (var state in _timelineRowRenderStates.Values)
            state.Canvas.Width = width;
    }

    private double GetSharedTimelineCanvasWidth(double requiredWidth = 0)
    {
        var contentWidth = Math.Max(requiredWidth, GetRequiredTimelineContentWidth());
        return Math.Max(contentWidth, GetMinimumTimelineCanvasWidth());
    }

    private double GetRequiredTimelineContentWidth()
    {
        var maxWidth = TimelineFirstItemLeft + TimelineRightPadding;

        foreach (var state in _timelineRowRenderStates.Values)
            maxWidth = Math.Max(maxWidth, state.RowWidth);

        return maxWidth;
    }

    public double GetMinimumTimelineCanvasWidth()
    {
        var viewportWidth = GetTimelineLayoutViewportWidth();
        var horizontalPadding = GetTimelineScrollViewerHorizontalPadding();

        return Math.Max(0, viewportWidth - horizontalPadding);
    }

    private double GetTimelineScrollViewerHorizontalPadding()
    {
        return TimelineScrollViewer == null
            ? 0
            : TimelineScrollViewer.Padding.Left + TimelineScrollViewer.Padding.Right;
    }

    private double GetTimelineLayoutViewportWidth()
    {
        var measuredViewportWidth = TimelineScrollViewer?.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer?.ActualWidth ?? 0;

        if (TimelineGrid == null || TimelineGrid.ActualWidth <= 0)
            return measuredViewportWidth;

        var headerWidth = GetDisplayTimelines().Count > 1
            ? TimelineHeaderWidth
            : 0;

        var expectedViewportWidth = Math.Max(0, TimelineGrid.ActualWidth - headerWidth);

        if (measuredViewportWidth <= 0)
            return expectedViewportWidth;

        if (expectedViewportWidth <= 0)
            return measuredViewportWidth;

        return Math.Min(measuredViewportWidth, expectedViewportWidth);
    }
}
