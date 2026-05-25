using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeySpammer;

public partial class MainWindow
{
    private void BeginDraggedStepGhost()
    {
        if (_drag.DraggedStepTimeline == null || _drag.DraggedStep == null)
            return;

        EndDraggedStepGhost();

        var ghost = CreateStepBlock(_drag.DraggedStepTimeline, _drag.DraggedStep);

        if (ghost is not FrameworkElement ghostElement)
            return;

        ghostElement.IsHitTestVisible = false;
        ghostElement.Opacity = MainWindow.DragUi.GhostOpacity;
        ghostElement.RenderTransformOrigin = new Point(0.5, 0.5);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(MainWindow.DragUi.GhostScale, MainWindow.DragUi.GhostScale));

        _draggedStepGhostTransform = new TranslateTransform();
        transformGroup.Children.Add(_draggedStepGhostTransform);

        ghostElement.RenderTransform = transformGroup;

        var size = MainWindow.MeasureTimelineItem(ghostElement);

        _draggedStepGhostWidth = size.Width;
        _draggedStepGhostHeight = size.Height;
        _draggedStepGhost = ghostElement;

        TimelineDragOverlayCanvas.Children.Add(ghostElement);

        Canvas.SetLeft(ghostElement, 0);
        Canvas.SetTop(ghostElement, 0);
        Panel.SetZIndex(ghostElement, 1000);

        UpdateDraggedStepGhostTargetPosition(snap: true);
        StartDragGhostAnimation();
    }

    private void UpdateDraggedStepGhostTargetPosition(bool snap = false)
    {
        if (_draggedStepGhost == null)
            return;

        if (!_drag.IsDraggingStep || _drag.DraggedStepTimeline == null)
            return;

        var mouse = Mouse.GetPosition(TimelineDragOverlayCanvas);

        var rowTopInRowsPanel = GetTimelineRowTopY(_drag.DraggedStepTimeline);

        var targetX = mouse.X - (_draggedStepGhostWidth / 2.0) + MainWindow.DragUi.GhostCursorOffsetX;
        var targetY = rowTopInRowsPanel + MainWindow.TimelineConnectorY - (_draggedStepGhostHeight / 2.0) + MainWindow.DragUi.GhostCursorOffsetY;

        _dragGhostTargetPosition = new Point(targetX, targetY);

        if (!snap)
            return;

        _dragGhostCurrentPosition = _dragGhostTargetPosition;
        ApplyDragGhostPosition();
    }

    private void StartDragGhostAnimation()
    {
        if (_isDragGhostAnimating)
            return;

        _isDragGhostAnimating = true;
        CompositionTarget.Rendering += DragGhost_Rendering;
    }

    private void StopDragGhostAnimation()
    {
        if (!_isDragGhostAnimating)
            return;

        _isDragGhostAnimating = false;
        CompositionTarget.Rendering -= DragGhost_Rendering;
    }

    private void DragGhost_Rendering(object? sender, EventArgs e)
    {
        if (_draggedStepGhostTransform == null || _draggedStepGhost == null)
            return;

        if (!_drag.IsDraggingStep)
            return;

        if (ApplyStepDragAutoScroll())
        {
            var currentPoint = Mouse.GetPosition(TimelineRowsPanel);
            var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

            if (previewChanged)
                RefreshTimelineDragPreview();
        }

        UpdateDraggedStepGhostTargetPosition();

        var followStrength = MainWindow.DragUi.GhostFollowStrength;

        var dx = _dragGhostTargetPosition.X - _dragGhostCurrentPosition.X;
        var dy = _dragGhostTargetPosition.Y - _dragGhostCurrentPosition.Y;

        if (Math.Abs((double)dx) < 0.2 && Math.Abs((double)dy) < 0.2)
        {
            _dragGhostCurrentPosition = _dragGhostTargetPosition;
        }
        else
        {
            _dragGhostCurrentPosition = new Point(
                _dragGhostCurrentPosition.X + (dx * followStrength),
                _dragGhostCurrentPosition.Y + (dy * followStrength));
        }

        ApplyDragGhostPosition();
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

    private void ApplyDragGhostPosition()
    {
        if (_draggedStepGhostTransform == null)
            return;

        _draggedStepGhostTransform.X = _dragGhostCurrentPosition.X;
        _draggedStepGhostTransform.Y = _dragGhostCurrentPosition.Y;
    }

    private void EndDraggedStepGhost()
    {
        StopDragGhostAnimation();

        if (_draggedStepGhost != null)
            TimelineDragOverlayCanvas.Children.Remove(_draggedStepGhost);

        _draggedStepGhost = null;
        _draggedStepGhostTransform = null;

        _draggedStepGhostWidth = 0;
        _draggedStepGhostHeight = 0;

        _dragGhostCurrentPosition = default;
        _dragGhostTargetPosition = default;
    }
}
