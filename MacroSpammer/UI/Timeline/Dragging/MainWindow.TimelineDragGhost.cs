using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void BeginDraggedStepGhost()
    {
        if (_drag.DraggedNodeTimeline == null || _drag.DraggedNode == null)
            return;

        EndDraggedStepGhost();

        var ghost = CreateDraggedStepGhostElement(_drag.DraggedNodeTimeline, _drag.DraggedNode);
        if (ghost is not FrameworkElement ghostElement)
            return;

        var size = MainWindow.MeasureTimelineItem(ghostElement);
        NodeDragGhost.Begin(
            ghostElement,
            size,
            MainWindow.DragUi.GhostOpacity,
            MainWindow.DragUi.GhostScale,
            GetDraggedStepGhostTargetPosition(size));

        StartDragGhostAnimation();
    }

    private UIElement CreateDraggedStepGhostElement(MacroTimeline timeline, MacroNode draggedNode)
    {
        var ghostSteps = GetDraggedDisplaySteps(timeline, draggedNode);
        if (ghostSteps.Count <= 1)
            return CreateNode(timeline, draggedNode);

        var canvas = new Canvas
        {
            Height = TimelineRowHeight,
            IsHitTestVisible = false
        };

        var currentLeft = 0.0;
        var maxHeight = 0.0;
        foreach (var step in ghostSteps)
        {
            var block = CreateNode(timeline, step);
            if (block is not FrameworkElement element)
                continue;

            element.IsHitTestVisible = false;
            var size = MainWindow.MeasureTimelineItem(element);
            Canvas.SetLeft(element, currentLeft);
            Canvas.SetTop(element, TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, size.Height));
            canvas.Children.Add(element);

            currentLeft += size.Width + TimelineItemGap;
            maxHeight = Math.Max(maxHeight, size.Height);
        }

        canvas.Width = Math.Max(1, currentLeft - TimelineItemGap);
        canvas.Height = Math.Max(1, maxHeight);
        return canvas;
    }

    private List<MacroNode> GetDraggedDisplaySteps(MacroTimeline timeline, MacroNode draggedNode)
    {
        if (!_selection.HasMultipleNodeSelection || !_selection.IsNodeSelected(timeline, draggedNode))
            return new List<MacroNode> { draggedNode };

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        return visibleSteps
            .Where(step => _selection.SelectedNodes.Any(selectedStep => IsSameSelectedStep(step, selectedStep)))
            .ToList();
    }

    private void UpdateDraggedStepGhostTargetPosition(bool snap = false)
    {
        if (!NodeDragGhost.HasGhost)
            return;

        if (!_drag.IsDraggingNode || _drag.DraggedNodeTimeline == null)
            return;

        NodeDragGhost.UpdateTarget(
            GetDraggedStepGhostTargetPosition(new Size(NodeDragGhost.Width, NodeDragGhost.Height)),
            snap);
    }

    private Point GetDraggedStepGhostTargetPosition(Size ghostSize)
    {
        if (_drag.DraggedNodeTimeline == null)
            return default;

        var mouse = Mouse.GetPosition(TimelineDragOverlayCanvas);
        var rowTopInRowsPanel = GetTimelineRowTopY(_drag.DraggedNodeTimeline);

        var targetX = mouse.X - (ghostSize.Width / 2.0) + MainWindow.DragUi.GhostCursorOffsetX;
        var targetY = rowTopInRowsPanel + MainWindow.TimelineConnectorY - (ghostSize.Height / 2.0) + MainWindow.DragUi.GhostCursorOffsetY;

        return new Point(targetX, targetY);
    }

    private void StartDragGhostAnimation()
    {
        NodeDragGhost.StartAnimation(DragGhost_Rendering);
    }

    private void StopDragGhostAnimation()
    {
        NodeDragGhost.StopAnimation(DragGhost_Rendering);
    }

    private void DragGhost_Rendering(object? sender, EventArgs e)
    {
        if (!NodeDragGhost.HasGhost)
            return;

        if (!_drag.IsDraggingNode)
            return;

        if (ApplyStepDragAutoScroll())
        {
            var currentPoint = Mouse.GetPosition(TimelineRowsPanel);
            var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

            if (previewChanged)
                RefreshTimelineDragPreview();
        }

        UpdateDraggedStepGhostTargetPosition();
        NodeDragGhost.MoveTowardTarget(MainWindow.DragUi.GhostFollowStrength);
    }

    private bool ApplyStepDragAutoScroll()
    {
        if (TimelineScrollViewer == null || TimelineRowsPanel == null)
            return false;

        if (!HasTimelineOverflow(TimelineScrollViewer.ExtentWidth, TimelineScrollViewer.ViewportWidth))
            return false;

        var mouse = Mouse.GetPosition(TimelineScrollViewer);
        var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
            ? TimelineScrollViewer.ViewportWidth
            : TimelineScrollViewer.ActualWidth;

        if (viewportWidth <= 0)
            return false;

        var edgeSize = Math.Min(MainWindow.DragUi.AutoScrollEdgeSize, viewportWidth / 2.0);
        if (edgeSize <= 0)
            return false;

        var scrollStep = 0.0;

        if (mouse.X < edgeSize)
        {
            var strength = (edgeSize - mouse.X) / edgeSize;
            scrollStep = -MainWindow.DragUi.AutoScrollMaxStep * Math.Clamp(strength, 0, 1);
        }
        else if (mouse.X > viewportWidth - edgeSize)
        {
            var strength = (mouse.X - (viewportWidth - edgeSize)) / edgeSize;
            scrollStep = MainWindow.DragUi.AutoScrollMaxStep * Math.Clamp(strength, 0, 1);
        }

        if (Math.Abs(scrollStep) < 0.1)
            return false;

        var previousOffset = TimelineScrollViewer.HorizontalOffset;
        var maxOffset = Math.Max(0, TimelineScrollViewer.ExtentWidth - TimelineScrollViewer.ViewportWidth);
        var targetOffset = Math.Clamp(previousOffset + scrollStep, 0, maxOffset);

        if (Math.Abs(targetOffset - previousOffset) < 0.1)
            return false;

        TimelineScrollViewer.ScrollToHorizontalOffset(targetOffset);
        UpdateTimelineScrollIndicator();
        return true;
    }

    private void EndDraggedStepGhost()
    {
        StopDragGhostAnimation();
        NodeDragGhost.End();
    }
}
