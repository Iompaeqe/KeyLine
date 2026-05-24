using System.Windows;
using System.Windows.Input;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private void AttachTimelineHeaderMouseHandlers(FrameworkElement element, MacroTimeline timeline)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            EnsureTimelineDragGlobalHandlers();

            SelectTimeline(timeline);
            _selection.SelectTimeline(timeline);

            _drag.BeginTimelineHeaderDrag(
                timeline,
                e.GetPosition(TimelineHeaderGrid),
                _document.Timelines.IndexOf(timeline));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (_drag.DraggedTimelineHeader == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(TimelineHeaderGrid);

            if (!_drag.IsDraggingTimelineHeader)
            {
                if (!_drag.ShouldStartTimelineHeaderDrag(currentPoint, TimelineHeaderDragThreshold))
                    return;

                _drag.MarkTimelineHeaderDragging();
            }

            MoveTimelineHeaderByMouseY(_drag.DraggedTimelineHeader, currentPoint.Y);

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            EndTimelineHeaderDrag(element);
            RefreshTimeline();

            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
                EndTimelineHeaderDrag(element);
        };
    }

    private void MoveTimelineHeaderByMouseY(MacroTimeline draggedTimeline, double mouseY)
    {
        var currentIndex = _document.Timelines.IndexOf(draggedTimeline);
        if (currentIndex < 0)
            return;

        var rowStride = TimelineRowHeight + TimelineRowGap;
        var deltaY = mouseY - _drag.HeaderDragStartPoint.Y;
        var targetIndex = _drag.HeaderDragStartIndex + (int)Math.Round(deltaY / rowStride);
        targetIndex = Math.Clamp(targetIndex, 0, _document.Timelines.Count - 1);

        if (targetIndex == currentIndex)
            return;

        _document.MoveTimeline(draggedTimeline, targetIndex);
        _selection.SelectTimeline(draggedTimeline);
        RefreshTimeline();
    }

    private void EndTimelineHeaderDrag(FrameworkElement element)
    {
        _drag.EndTimelineHeaderDrag();

        if (element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }
}
