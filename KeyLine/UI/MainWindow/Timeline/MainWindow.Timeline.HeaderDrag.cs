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
    // Multi-timeline (group) drag state, captured when a header drag actually begins.
    private MacroTimeline? _pendingTimelineClick;
    private List<MacroTimeline>? _timelineDragBaseOrder;
    private List<MacroTimeline>? _timelineDragGroup;

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

            if (e.ClickCount >= 2)
            {
                SelectSingleTimelineFromHeader(timeline);
                OpenInspectorFromSelection();
                e.Handled = true;
                return;
            }

            ApplyTimelineHeaderSelection(timeline, Keyboard.Modifiers);

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
                BeginTimelineHeaderDragGroup(_drag.DraggedTimelineHeader);
            }

            MoveTimelineHeaderByMouseY(_drag.DraggedTimelineHeader, currentPoint.Y);

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            var wasDragging = _drag.IsDraggingTimelineHeader;
            EndTimelineHeaderDrag(element);

            // A plain click on an already-selected member of a multi-selection defers the collapse
            // to here, so a drag could use the whole group. No drag happened, so collapse now.
            if (!wasDragging && _pendingTimelineClick != null)
                SelectSingleTimelineFromHeader(_pendingTimelineClick);

            _pendingTimelineClick = null;
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

    // --- Selection helpers (shared by click handlers) ---

    private void ApplyTimelineHeaderSelection(MacroTimeline timeline, ModifierKeys modifiers)
    {
        _pendingTimelineClick = null;
        var isHook = IsHookTimeline(timeline);

        if (!isHook && modifiers.HasFlag(ModifierKeys.Control))
        {
            _selection.ToggleTimelineSelection(timeline);
            SyncActiveTimelineToSelection(timeline);
            RefreshInspector();
            return;
        }

        if (!isHook && modifiers.HasFlag(ModifierKeys.Shift) &&
            _selection.AnchorTimeline != null && !IsHookTimeline(_selection.AnchorTimeline))
        {
            SelectTimelineRange(_selection.AnchorTimeline, timeline);
            SyncActiveTimelineToSelection(timeline);
            RefreshInspector();
            return;
        }

        // Plain click on an already-selected member of a multi-selection: defer the collapse to
        // mouse-up so dragging can move the whole group.
        if (!isHook && _selection.HasMultipleTimelineSelection && _selection.IsTimelineSelected(timeline))
        {
            _pendingTimelineClick = timeline;
            SelectTimeline(timeline, refreshInspector: false);
            RefreshInspector();
            return;
        }

        SelectSingleTimelineFromHeader(timeline);
    }

    private void SelectSingleTimelineFromHeader(MacroTimeline timeline)
    {
        _selection.SelectTimeline(timeline);
        SelectTimeline(timeline, refreshInspector: false);
        RefreshInspector();
    }

    // Keeps the document's active timeline pointing at a selected timeline (preferring the clicked one)
    // so node-add context stays meaningful while multiple timelines are selected.
    private void SyncActiveTimelineToSelection(MacroTimeline clicked)
    {
        var active = _selection.IsTimelineSelected(clicked)
            ? clicked
            : _selection.SelectedTimelines.LastOrDefault();

        if (active != null)
            SelectTimeline(active, refreshInspector: false);
    }

    // Selects the inclusive range of normal timelines between anchor and target (display order).
    private void SelectTimelineRange(MacroTimeline anchor, MacroTimeline target)
    {
        var timelines = _document.Timelines;
        var anchorIndex = timelines.IndexOf(anchor);
        var targetIndex = timelines.IndexOf(target);

        if (anchorIndex < 0 || targetIndex < 0)
        {
            _selection.SelectTimeline(target);
            return;
        }

        var lo = Math.Min(anchorIndex, targetIndex);
        var hi = Math.Max(anchorIndex, targetIndex);

        var range = new List<MacroTimeline>();
        for (var i = lo; i <= hi; i++)
            range.Add(timelines[i]);

        _selection.SelectTimelines(range, anchor);
    }

    // --- Group drag ---

    private void BeginTimelineHeaderDragGroup(MacroTimeline draggedTimeline)
    {
        _pendingTimelineClick = null;
        _timelineDragBaseOrder = _document.Timelines.ToList();

        // Drag the whole selected group only when the dragged timeline is part of a multi-selection.
        // Hooks are never part of a group reorder.
        if (_selection.HasMultipleTimelineSelection &&
            _selection.IsTimelineSelected(draggedTimeline) &&
            !IsHookTimeline(draggedTimeline))
        {
            _timelineDragGroup = _document.Timelines
                .Where(t => _selection.IsTimelineSelected(t) && !IsHookTimeline(t))
                .ToList();
        }
        else
        {
            _timelineDragGroup = new List<MacroTimeline> { draggedTimeline };
        }
    }

    private void MoveTimelineHeaderByMouseY(MacroTimeline draggedTimeline, double mouseY)
    {
        if (_timelineDragBaseOrder == null || _timelineDragGroup == null)
            return;

        var rowStride = GetDisplayRowHeight(draggedTimeline) + GetDisplayRowGap(draggedTimeline);
        var deltaY = mouseY - _drag.HeaderDragStartPoint.Y;
        var indexDelta = (int)Math.Round(deltaY / rowStride);
        var desiredDraggedIndex = _drag.HeaderDragStartIndex + indexDelta;

        if (_timelineDragGroup.Count <= 1)
        {
            var targetIndex = Math.Clamp(desiredDraggedIndex, 0, _document.Timelines.Count - 1);
            if (targetIndex == _document.Timelines.IndexOf(draggedTimeline))
                return;

            _document.MoveTimeline(draggedTimeline, targetIndex);
            _selection.SelectTimeline(draggedTimeline);
        }
        else
        {
            var newOrder = ComputeTimelineGroupReorder(
                _timelineDragBaseOrder,
                _timelineDragGroup,
                draggedTimeline,
                desiredDraggedIndex);

            if (newOrder == null || newOrder.SequenceEqual(_document.Timelines))
                return;

            _document.ReorderTimelines(newOrder);
            _selection.SelectTimelines(_timelineDragGroup, draggedTimeline);
        }

        RefreshInspector();
        RefreshTimeline();
        ScheduleSaveState();
    }

    // Produces a new timeline order with the selected group moved together (relative order preserved)
    // so the dragged timeline lands at desiredDraggedIndex. Computed from a stable base order so live
    // dragging never compounds.
    private static List<MacroTimeline>? ComputeTimelineGroupReorder(
        IReadOnlyList<MacroTimeline> baseOrder,
        IReadOnlyList<MacroTimeline> group,
        MacroTimeline draggedTimeline,
        int desiredDraggedIndex)
    {
        var groupSet = group.ToHashSet();
        var remaining = baseOrder.Where(t => !groupSet.Contains(t)).ToList();

        var draggedPosInGroup = group.ToList().IndexOf(draggedTimeline);
        if (draggedPosInGroup < 0)
            return null;

        var desiredGroupStart = Math.Clamp(
            desiredDraggedIndex - draggedPosInGroup,
            0,
            Math.Max(0, baseOrder.Count - group.Count));

        var insertIndex = Math.Clamp(desiredGroupStart, 0, remaining.Count);

        var result = new List<MacroTimeline>(baseOrder.Count);
        result.AddRange(remaining.Take(insertIndex));
        result.AddRange(group);
        result.AddRange(remaining.Skip(insertIndex));
        return result;
    }

    private void EndTimelineHeaderDrag(FrameworkElement element)
    {
        _drag.EndTimelineHeaderDrag();
        _timelineDragBaseOrder = null;
        _timelineDragGroup = null;

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
