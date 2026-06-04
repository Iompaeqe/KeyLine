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
    private void RefreshTimelineRow(MacroTimeline timeline)
    {
        if (TimelineRowsPanel == null)
            return;

        var rowIndex = _document.Timelines.IndexOf(timeline);
        if (rowIndex < 0 || rowIndex >= TimelineRowsPanel.Children.Count)
        {
            RefreshTimeline();
            return;
        }

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            GetTimelineRenderRawSteps(timeline).ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var canvasWidth = GetExistingTimelineCanvasWidth(rowIndex);

        var replacementRow = CreateTimelineRow(
            timeline,
            visibleSteps,
            canvasWidth,
            rowIndex == 0,
            rowIndex == _document.Timelines.Count - 1);

        TimelineRowsPanel.Children.RemoveAt(rowIndex);
        TimelineRowsPanel.Children.Insert(rowIndex, replacementRow);

        FitTimelineCanvasWidthToCurrentContent();
        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void UpdateSelectionVisuals(MacroTimeline? previousTimeline, MacroTimeline? currentTimeline)
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

    private void AppendRecordedStepsToTimelineRow(MacroTimeline timeline, IReadOnlyList<MacroNode> addedRawSteps)
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

            AddTimelineItem(state.Canvas, item);
            state.VisualItems.Add(item);
            if (item.AnimationKey != null)
                state.VisualItemsByAnimationKey[item.AnimationKey] = item;
            UpdateRowConnectorBounds(state, item);

            currentLeft += size.Width + TimelineItemGap;
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

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
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
            state.Connector = new Border
            {
                Height = TimelineConnectorThickness,
                CornerRadius = new CornerRadius(TimelineConnectorThickness / 2.0),
                Background = new SolidColorBrush(Color.FromRgb(31, 48, 66)),
                Opacity = 0.85,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(state.Connector, firstCenterX);
            Canvas.SetTop(
                state.Connector,
                TimelineLayoutCalculator.GetConnectorTop(TimelineConnectorY, TimelineConnectorThickness));
            Panel.SetZIndex(state.Connector, -10);
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

    private void EnsureTimelineCanvasWidthForAllRows(double requiredWidth)
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

    private double GetMinimumTimelineCanvasWidth()
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

        var headerWidth = _document.Timelines.Count > 1
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
