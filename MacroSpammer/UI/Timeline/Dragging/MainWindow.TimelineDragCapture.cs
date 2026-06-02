using System.Windows;
using System.Windows.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    private void EnsureTimelineDragGlobalHandlers()
    {
        if (_timelineDragGlobalHandlersAttached)
            return;

        _timelineDragGlobalHandlersAttached = true;

        AddHandler(
            Mouse.PreviewMouseUpEvent,
            new MouseButtonEventHandler(Window_PreviewMouseUpForTimelineDrag),
            true);

        AddHandler(
            Mouse.PreviewMouseMoveEvent,
            new MouseEventHandler(Window_PreviewMouseMoveForTimelineDrag),
            true);
    }

    private void BeginWindowLevelStepDragCapture(FrameworkElement originalElement)
    {
        if (originalElement.IsMouseCaptured)
            originalElement.ReleaseMouseCapture();

        Mouse.Capture(this, CaptureMode.SubTree);
    }

    private void Window_PreviewMouseMoveForTimelineDrag(object sender, MouseEventArgs e)
    {
        if (!_drag.IsDraggingNode ||
            _drag.DraggedNodeTimeline == null ||
            _drag.DraggedNode == null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPoint = e.GetPosition(TimelineRowsPanel);
        var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

        if (previewChanged)
            RefreshTimelineDragPreview();

        UpdateDraggedStepGhostTargetPosition();

        e.Handled = true;
    }

    private void Window_PreviewMouseUpForTimelineDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (!_drag.IsDraggingNode && !_drag.IsDraggingTimelineHeader)
            return;

        CompleteStepDrop();

        e.Handled = true;
    }
}
