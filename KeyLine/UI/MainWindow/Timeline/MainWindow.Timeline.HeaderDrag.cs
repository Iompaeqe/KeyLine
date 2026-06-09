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
    private void AttachTimelineHeaderMouseHandlers(FrameworkElement element, MacroTimeline timeline)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // The collapse chevron lives inside the draggable header, so intercept its click here
            // (the preview/tunneling phase) before any drag or selection begins.
            if (IsCollapseToggleSource(e.OriginalSource as DependencyObject))
            {
                ToggleTimelineCollapsed(timeline);
                e.Handled = true;
                return;
            }

            EnsureTimelineDragGlobalHandlers();

            if (ReferenceEquals(_pendingDeleteTimeline, timeline))
            {
                ConfirmTimelineDelete(timeline);
                e.Handled = true;
                return;
            }

            ResetTimelineDeleteConfirmation();

            SelectTimeline(timeline);
            _selection.SelectTimeline(timeline);
            RefreshInspector();

            if (e.ClickCount >= 2)
            {
                OpenInspectorFromSelection();
                e.Handled = true;
                return;
            }

            if (!_isTimelineEditingEnabled || IsHookTimeline(timeline))
            {
                // Hook timelines are selectable but pinned (not drag-reorderable).
                e.Handled = true;
                return;
            }

            _drag.BeginTimelineHeaderDrag(
                timeline,
                e.GetPosition(TimelineHeaderGrid),
                _document.Timelines.IndexOf(timeline));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            CancelTimelineDragState();

            if (!_isTimelineEditingEnabled || IsHookTimeline(timeline))
            {
                // Hook timelines cannot be deleted; middle-click just selects them.
                SelectTimeline(timeline);
                _selection.SelectTimeline(timeline);
                RefreshInspector();
                e.Handled = true;
                return;
            }

            if (ReferenceEquals(_pendingDeleteTimeline, timeline))
                ConfirmTimelineDelete(timeline);
            else
                BeginTimelineDeleteConfirmation(timeline);

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (!_isTimelineEditingEnabled)
                return;

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

                SaveDocumentUndoSnapshot();
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

        element.PreviewMouseRightButtonDown += (_, e) =>
        {
            CancelTimelineDragState();
            var clearedPendingDelete = ResetTimelineDeleteConfirmation();

            SelectTimeline(timeline);
            _selection.SelectTimeline(timeline);

            if (clearedPendingDelete)
                RefreshTimeline();
            else
                RefreshInspector();
        };
    }

    private void MoveTimelineHeaderByMouseY(MacroTimeline draggedTimeline, double mouseY)
    {
        var currentIndex = _document.Timelines.IndexOf(draggedTimeline);
        if (currentIndex < 0)
            return;

        var rowStride = GetDisplayRowHeight(draggedTimeline) + GetDisplayRowGap(draggedTimeline);
        var deltaY = mouseY - _drag.HeaderDragStartPoint.Y;
        var targetIndex = _drag.HeaderDragStartIndex + (int)Math.Round(deltaY / rowStride);
        targetIndex = Math.Clamp(targetIndex, 0, _document.Timelines.Count - 1);

        if (targetIndex == currentIndex)
            return;

        _document.MoveTimeline(draggedTimeline, targetIndex);
        _selection.SelectTimeline(draggedTimeline);
        RefreshInspector();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void EndTimelineHeaderDrag(FrameworkElement element)
    {
        _drag.EndTimelineHeaderDrag();

        if (element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }

    private void BeginTimelineHeaderRename(MacroTimeline timeline)
    {
        if (!_isTimelineEditingEnabled || IsHookTimeline(timeline))
            return;

        ResetTimelineDeleteConfirmation();
        SelectTimeline(timeline);
        _selection.SelectTimeline(timeline);
        RefreshTimeline();
        BeginTimelineNameEditFromHeader(timeline);
    }

    private void BeginTimelineDeleteConfirmation(MacroTimeline timeline)
    {
        if (_document.Timelines.Count <= 1 || IsHookTimeline(timeline))
            return;

        SelectTimeline(timeline);
        _selection.SelectTimeline(timeline);
        RefreshInspector();

        _pendingDeleteTimeline = timeline;
        RefreshTimeline();
    }

    private void ConfirmTimelineDelete(MacroTimeline timeline)
    {
        if (!ReferenceEquals(_pendingDeleteTimeline, timeline))
            return;

        _pendingDeleteTimeline = null;
        DeleteSelectedTimeline(timeline);
    }

    private bool ResetTimelineDeleteConfirmation()
    {
        if (_pendingDeleteTimeline == null)
            return false;

        _pendingDeleteTimeline = null;
        return true;
    }

    private bool IsSourcePendingDeleteTimelineHeader(DependencyObject? source)
    {
        if (_pendingDeleteTimeline == null)
            return false;

        while (source != null)
        {
            if (source is FrameworkElement element &&
                ReferenceEquals(element.Tag, _pendingDeleteTimeline))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
