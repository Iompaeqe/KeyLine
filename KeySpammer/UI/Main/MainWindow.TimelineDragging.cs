using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow
{
    private sealed class StepDragTarget
    {
        public required MacroStep RawInsertAnchor { get; init; }
        public required double CenterX { get; init; }
    }

    private readonly List<StepDragTarget> _stepDragTargets = new();

    private readonly List<MacroStep> _stepDragPreviewRawSteps = new();
    private readonly List<MacroStep> _stepDragRawItems = new();

    // Drag-preview layout cache.
    // Without this, every mouse move creates/measures WPF controls just to decide
    // whether the placeholder should move. That is the main performance killer.
    private readonly Dictionary<MacroStep, double> _stepDragPreviewWidthByFirstRawItem = new();
    private double _stepDragPreviewContentLeftX;

    private FrameworkElement? _draggedStepGhost;
    private TranslateTransform? _draggedStepGhostTransform;

    private double _draggedStepGhostWidth;
    private double _draggedStepGhostHeight;

    private Point? _lastDroppedGhostRowsPanelPosition;
    
    private Point _dragGhostCurrentPosition;
    private Point _dragGhostTargetPosition;

    private bool _isDragGhostAnimating;
    private bool _timelineDragGlobalHandlersAttached;

    private const double StepDragThreshold = 6;
    private const double TimelineHeaderDragThreshold = 6;

    private void AttachStepMouseHandlers(FrameworkElement element, MacroTimeline timeline, MacroStep step)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            EnsureTimelineDragGlobalHandlers();

            SelectTimeline(timeline);
            _selection.SelectStep(timeline, step);
            _drag.BeginStepDrag(timeline, step, e.GetPosition(TimelineRowsPanel));

            element.CaptureMouse();

            e.Handled = true;
        };

        element.PreviewMouseMove += (_, e) =>
        {
            if (_drag.DraggedStep == null ||
                _drag.DraggedStepTimeline == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(TimelineRowsPanel);

            if (!_drag.IsDraggingStep)
            {
                if (!_drag.ShouldStartStepDrag(currentPoint, StepDragThreshold))
                    return;

                _drag.MarkStepDragging();

                BeginStepDragPreviewModel(_drag.DraggedStepTimeline, _drag.DraggedStep);
                UpdateStepDragPreviewFromMouse(currentPoint);

                BeginWindowLevelStepDragCapture(element);
                BeginDraggedStepGhost();

                RefreshTimelineDragPreview();
            }

            var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

            if (previewChanged)
                RefreshTimelineDragPreview();

            UpdateDraggedStepGhostTargetPosition();

            e.Handled = true;
        };

        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            CompleteStepDrop();
            e.Handled = true;
        };

        element.LostMouseCapture += (_, _) =>
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed && _drag.DraggedStep != null)
                CancelTimelineDragState();
        };
    }

    private void BeginDraggedStepGhost()
    {
        if (_drag.DraggedStepTimeline == null || _drag.DraggedStep == null)
            return;

        EndDraggedStepGhost();

        var ghost = CreateStepBlock(_drag.DraggedStepTimeline, _drag.DraggedStep);

        if (ghost is not FrameworkElement ghostElement)
            return;

        ghostElement.IsHitTestVisible = false;
        ghostElement.Opacity = 0.86;
        ghostElement.RenderTransformOrigin = new Point(0.5, 0.5);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(1.04, 1.04));

        _draggedStepGhostTransform = new TranslateTransform();
        transformGroup.Children.Add(_draggedStepGhostTransform);

        ghostElement.RenderTransform = transformGroup;

        var size = MeasureTimelineItem(ghostElement);

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

        var targetX = mouse.X - (_draggedStepGhostWidth / 2.0);
        var targetY = rowTopInRowsPanel + TimelineConnectorY - (_draggedStepGhostHeight / 2.0);

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

        UpdateDraggedStepGhostTargetPosition();

        const double followStrength = 0.65;

        var dx = _dragGhostTargetPosition.X - _dragGhostCurrentPosition.X;
        var dy = _dragGhostTargetPosition.Y - _dragGhostCurrentPosition.Y;

        if (Math.Abs(dx) < 0.2 && Math.Abs(dy) < 0.2)
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

    private sealed class StepPreviewSlot
    {
        public required List<MacroStep> RawItems { get; init; }
        public required double CenterX { get; init; }
        public required bool IsDraggedSlot { get; init; }
    }

    private void BeginStepDragPreviewModel(MacroTimeline timeline, MacroStep draggedStep)
    {
        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _stepDragPreviewWidthByFirstRawItem.Clear();

        var draggedItems = GetRawStepsForDisplayStep(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        _stepDragRawItems.AddRange(draggedItems);
        _stepDragPreviewRawSteps.AddRange(timeline.Steps);

        BuildStepDragPreviewLayoutCache(timeline);

        _drag.UpdateStepDragPreview(_drag.StepDragCurrentPoint, GetStepDragPreviewRawInsertAnchor());
    }

    private void BuildStepDragPreviewLayoutCache(MacroTimeline timeline)
    {
        _stepDragPreviewWidthByFirstRawItem.Clear();

        // Timeline rows are laid out at X=0 inside TimelineRowsPanel.
        // The canvas starts after the optional header column. Do not use
        // TransformToAncestor here during dragging; it is unnecessary work.
        _stepDragPreviewContentLeftX = _document.Timelines.Count > 1
            ? TimelineHeaderWidth
            : 0;

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _stepDragPreviewRawSteps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        foreach (var displayStep in visibleSteps)
        {
            var rawItems = GetRawStepsForDisplayStep(_stepDragPreviewRawSteps, displayStep);
            if (rawItems.Count == 0)
                continue;

            CacheStepPreviewWidth(timeline, displayStep, rawItems);
        }
    }

    private double GetCachedStepPreviewWidth(
        MacroTimeline timeline,
        MacroStep displayStep,
        List<MacroStep> rawItems)
    {
        if (rawItems.Count == 0)
            return 72;

        var firstRawItem = rawItems[0];

        if (_stepDragPreviewWidthByFirstRawItem.TryGetValue(firstRawItem, out var width))
            return width;

        return CacheStepPreviewWidth(timeline, displayStep, rawItems);
    }

    private double CacheStepPreviewWidth(
        MacroTimeline timeline,
        MacroStep displayStep,
        List<MacroStep> rawItems)
    {
        if (rawItems.Count == 0)
            return 72;

        var firstRawItem = rawItems[0];

        if (_stepDragPreviewWidthByFirstRawItem.TryGetValue(firstRawItem, out var existingWidth))
            return existingWidth;

        var isDraggedSlot = rawItems.Any(_stepDragRawItems.Contains);

        var block = isDraggedSlot && _drag.DraggedStep != null
            ? CreateStepBlock(timeline, _drag.DraggedStep)
            : CreateStepBlock(timeline, displayStep);

        var size = MeasureTimelineItem(block);
        var width = Math.Max(1, size.Width);

        _stepDragPreviewWidthByFirstRawItem[firstRawItem] = width;
        return width;
    }

    private bool UpdateStepDragPreviewFromMouse(Point currentPoint)
    {
        var orderChanged = UpdateStepDragPreviewOrderFromMouse(currentPoint.X);
        var rawInsertAnchor = GetStepDragPreviewRawInsertAnchor();
        var anchorChanged = _drag.UpdateStepDragPreview(currentPoint, rawInsertAnchor);

        return orderChanged || anchorChanged;
    }

    private bool UpdateStepDragPreviewOrderFromMouse(double mouseX)
    {
        if (_drag.DraggedStepTimeline == null || _stepDragPreviewRawSteps.Count == 0 || _stepDragRawItems.Count == 0)
            return false;

        var changed = false;
        var maxMoves = Math.Max(1, _stepDragPreviewRawSteps.Count);

        for (var move = 0; move < maxMoves; move++)
        {
            var slots = BuildStepPreviewSlots(_drag.DraggedStepTimeline);
            var draggedSlotIndex = slots.FindIndex(slot => slot.IsDraggedSlot);

            if (draggedSlotIndex < 0)
                break;

            if (draggedSlotIndex > 0 && mouseX < slots[draggedSlotIndex - 1].CenterX)
            {
                MoveDraggedPreviewBeforeRawAnchor(slots[draggedSlotIndex - 1].RawItems[0]);
                changed = true;
                continue;
            }

            if (draggedSlotIndex < slots.Count - 1 && mouseX > slots[draggedSlotIndex + 1].CenterX)
            {
                var anchorAfterRightNeighbor = draggedSlotIndex + 2 < slots.Count
                    ? slots[draggedSlotIndex + 2].RawItems[0]
                    : null;

                MoveDraggedPreviewBeforeRawAnchor(anchorAfterRightNeighbor);
                changed = true;
                continue;
            }

            break;
        }

        return changed;
    }

    private List<StepPreviewSlot> BuildStepPreviewSlots(MacroTimeline timeline)
    {
        var slots = new List<StepPreviewSlot>();

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _stepDragPreviewRawSteps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var currentLeft = TimelineFirstItemLeft;

        foreach (var displayStep in visibleSteps)
        {
            var rawItems = GetRawStepsForDisplayStep(_stepDragPreviewRawSteps, displayStep);
            if (rawItems.Count == 0)
                continue;

            var width = GetCachedStepPreviewWidth(timeline, displayStep, rawItems);

            slots.Add(new StepPreviewSlot
            {
                RawItems = rawItems,
                CenterX = _stepDragPreviewContentLeftX + currentLeft + (width / 2.0),
                IsDraggedSlot = rawItems.Any(_stepDragRawItems.Contains)
            });

            currentLeft += width + TimelineItemGap;
        }

        return slots;
    }

    private void MoveDraggedPreviewBeforeRawAnchor(MacroStep? rawInsertAnchor)
    {
        if (_stepDragPreviewRawSteps.Count == 0 || _stepDragRawItems.Count == 0)
            return;

        foreach (var draggedItem in _stepDragRawItems)
            _stepDragPreviewRawSteps.Remove(draggedItem);

        var insertIndex = rawInsertAnchor == null
            ? _stepDragPreviewRawSteps.Count
            : _stepDragPreviewRawSteps.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = _stepDragPreviewRawSteps.Count;

        for (var i = 0; i < _stepDragRawItems.Count; i++)
            _stepDragPreviewRawSteps.Insert(insertIndex + i, _stepDragRawItems[i]);
    }

    private MacroStep? GetStepDragPreviewRawInsertAnchor()
    {
        if (_stepDragPreviewRawSteps.Count == 0 || _stepDragRawItems.Count == 0)
            return null;

        var draggedFirstIndex = _stepDragPreviewRawSteps.IndexOf(_stepDragRawItems[0]);
        if (draggedFirstIndex < 0)
            return null;

        var afterDraggedIndex = draggedFirstIndex + _stepDragRawItems.Count;

        for (var i = afterDraggedIndex; i < _stepDragPreviewRawSteps.Count; i++)
        {
            var candidate = _stepDragPreviewRawSteps[i];
            if (!_stepDragRawItems.Contains(candidate))
                return candidate;
        }

        return null;
    }

    private IReadOnlyList<MacroStep> GetTimelineRenderRawSteps(MacroTimeline timeline)
    {
        if (_drag.IsDraggingStep &&
            ReferenceEquals(_drag.DraggedStepTimeline, timeline) &&
            _stepDragPreviewRawSteps.Count > 0)
        {
            return _stepDragPreviewRawSteps;
        }

        return timeline.Steps;
    }

    private List<MacroStep> GetRawStepsForDisplayStep(IReadOnlyCollection<MacroStep> rawSteps, MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(rawSteps.Contains)
                .ToList();
        }

        return rawSteps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }

    private double GetTimelineRowTopY(MacroTimeline timeline)
    {
        var index = _document.Timelines.IndexOf(timeline);
        if (index < 0)
            return 0;

        return index * (TimelineRowHeight + TimelineRowGap);
    }


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
        if (!_drag.IsDraggingStep ||
            _drag.DraggedStepTimeline == null ||
            _drag.DraggedStep == null ||
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

        if (_drag.DraggedStep == null && _drag.DraggedTimelineHeader == null)
            return;

        CompleteStepDrop();

        e.Handled = true;
    }

    private void CompleteStepDrop()
    {
        if (_drag.IsDraggingStep && _drag.DraggedStepTimeline != null && _drag.DraggedStep != null)
        {
            CaptureDroppedGhostPositionForAnimation();
            
            MoveStepBeforeRawAnchor(
                _drag.DraggedStepTimeline,
                _drag.DraggedStep,
                _drag.StepDropRawInsertAnchor);
            
            SeedDraggedNodeAnimationFromGhost();
        }

        CancelTimelineDragState();
        RefreshTimeline();

        _lastDroppedGhostRowsPanelPosition = null;
    }

    private void CancelTimelineDragState()
    {
        EndDraggedStepGhost();

        _stepDragTargets.Clear();
        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _stepDragPreviewWidthByFirstRawItem.Clear();

        _drag.EndStepDrag();
        _drag.EndTimelineHeaderDrag();
        _drag.EndTimelinePan();

        if (Mouse.Captured != null)
            Mouse.Capture(null);
    }

    private void AttachTimelineHeaderMouseHandlers(FrameworkElement element, MacroTimeline timeline)
    {
        element.PreviewMouseLeftButtonDown += (_, e) =>
        {
            EnsureTimelineDragGlobalHandlers();

            SelectTimeline(timeline);
            _selection.SelectTimeline(timeline);

            _drag.BeginTimelineHeaderDrag(
                timeline,
                e.GetPosition(TimelineRowsPanel),
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

            var currentPoint = e.GetPosition(TimelineRowsPanel);

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

    private void EndStepDrag(FrameworkElement? element = null)
    {
        EndDraggedStepGhost();

        _stepDragTargets.Clear();
        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _stepDragPreviewWidthByFirstRawItem.Clear();
        _drag.EndStepDrag();

        if (element?.IsMouseCaptured == true)
            element.ReleaseMouseCapture();

        if (Mouse.Captured == this)
            Mouse.Capture(null);
    }

    private void EndTimelineHeaderDrag(FrameworkElement element)
    {
        _drag.EndTimelineHeaderDrag();

        if (element.IsMouseCaptured)
            element.ReleaseMouseCapture();
    }

    private void MoveStepBeforeRawAnchor(
        MacroTimeline timeline,
        MacroStep draggedStep,
        MacroStep? rawInsertAnchor)
    {
        var draggedItems = GetRawStepsForDisplayStep(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        if (rawInsertAnchor != null && draggedItems.Contains(rawInsertAnchor))
            return;

        var oldFirstIndex = timeline.Steps.IndexOf(draggedItems[0]);

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? timeline.Steps.Count
            : timeline.Steps.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = timeline.Steps.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return;

        RemoveDraggedItems(timeline, draggedItems);

        var insertIndex = rawInsertAnchor == null
            ? timeline.Steps.Count
            : timeline.Steps.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = timeline.Steps.Count;

        InsertDraggedItems(timeline, insertIndex, draggedItems);

        _selection.SelectStep(timeline, draggedStep);
    }

    private double GetTimelineContentLeftX(MacroTimeline timeline)
    {
        foreach (var row in TimelineRowsPanel.Children.OfType<Grid>())
        {
            if (!ReferenceEquals(row.Tag, timeline))
                continue;

            var rowPosition = row.TransformToAncestor(TimelineRowsPanel).Transform(new Point(0, 0));
            var headerWidth = _document.Timelines.Count > 1 ? TimelineHeaderWidth : 0;
            return rowPosition.X + headerWidth;
        }

        return _document.Timelines.Count > 1 ? TimelineHeaderWidth : 0;
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

    private void MoveStepBeforeDisplayStep(MacroTimeline timeline, MacroStep draggedStep, MacroStep? insertBeforeDisplayStep)
    {
        var draggedItems = GetRawStepsForDisplayStep(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        var oldFirstIndex = timeline.Steps.IndexOf(draggedItems[0]);
        var rawInsertAnchor = GetRawInsertAnchor(timeline, insertBeforeDisplayStep, draggedItems);

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? timeline.Steps.Count
            : timeline.Steps.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = timeline.Steps.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return;

        RemoveDraggedItems(timeline, draggedItems);

        var insertIndex = GetInsertIndex(timeline, rawInsertAnchor);
        InsertDraggedItems(timeline, insertIndex, draggedItems);

        RefreshTimeline();
        _selection.SelectStep(timeline, draggedStep);
    }

    private MacroStep? GetRawInsertAnchor(MacroTimeline timeline, MacroStep? insertBeforeDisplayStep, List<MacroStep> draggedItems)
    {
        if (insertBeforeDisplayStep == null)
            return null;

        var targetItems = GetRawStepsForDisplayStep(timeline, insertBeforeDisplayStep);

        if (targetItems.Count == 0)
            return null;

        return draggedItems.Any(targetItems.Contains)
            ? null
            : targetItems[0];
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private void RemoveDraggedItems(MacroTimeline timeline, IEnumerable<MacroStep> draggedItems)
    {
        foreach (var item in draggedItems)
            timeline.Steps.Remove(item);
    }

    private int GetInsertIndex(MacroTimeline timeline, MacroStep? rawInsertAnchor)
    {
        if (rawInsertAnchor == null)
            return timeline.Steps.Count;

        var insertIndex = timeline.Steps.IndexOf(rawInsertAnchor);
        return insertIndex < 0 ? timeline.Steps.Count : insertIndex;
    }

    private void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroStep> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Steps.Insert(insertIndex + i, draggedItems[i]);
    }

    private List<MacroStep> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(sourceStep => timeline.Steps.Contains(sourceStep))
                .ToList();
        }

        return timeline.Steps.Contains(step)
            ? new List<MacroStep> { step }
            : new List<MacroStep>();
    }
    private void CaptureDroppedGhostPositionForAnimation()
    {
        if (_draggedStepGhostTransform == null || _drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
        {
            _lastDroppedGhostRowsPanelPosition = null;
            return;
        }

        _lastDroppedGhostRowsPanelPosition = new Point(
            _draggedStepGhostTransform.X,
            TimelineConnectorY - (_draggedStepGhostHeight / 2.0));
    }

    private void SeedDraggedNodeAnimationFromGhost()
    {
        if (_lastDroppedGhostRowsPanelPosition == null || _drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
        {
            return;
        }

        var animationKey = GetTimelineAnimationKey(_drag.DraggedStepTimeline, _drag.DraggedStep);
        _timelineVisualPositions[animationKey] = _lastDroppedGhostRowsPanelPosition.Value;
    }
}
