using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void BeginStepDragPreviewModel(MacroTimeline timeline, MacroNode draggedNode)
    {
        _stepDragPreviewSlots.Clear();
        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(timeline));

        var draggedItems = GetRawStepsForDrag(timeline, draggedNode);
        if (draggedItems.Count == 0)
            return;

        _stepDragRawItems.AddRange(draggedItems);
        _stepDragPreviewSlots.AddRange(BuildPreviewSlots(timeline, timeline.Steps, _stepDragRawItems));
        SyncStepDragPreviewRawSteps();

        _drag.UpdateStepDragPreview(_drag.StepDragCurrentPoint, GetStepDragPreviewRawInsertAnchor());
    }

    private List<NodePreviewSlot> BuildPreviewSlots(
        MacroTimeline timeline,
        IReadOnlyCollection<MacroNode> rawNodes,
        IReadOnlyCollection<MacroNode> draggedItems)
    {
        _stepDragPreviewContentLeftX = 0;

        var visibleNodes = MacroTimelineBuilder.BuildVisibleSteps(
            rawNodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var slots = new List<NodePreviewSlot>();
        foreach (var displayNode in visibleNodes)
        {
            var rawItems = GetRawStepsForDisplayStep(rawNodes, displayNode);
            if (rawItems.Count == 0)
                continue;

            var measuredNode = rawItems.Any(draggedItems.Contains) && _drag.DraggedStep != null
                ? _drag.DraggedStep
                : displayNode;

            slots.Add(new NodePreviewSlot
            {
                DisplayNode = displayNode,
                RawItems = rawItems,
                Width = Math.Max(1, MeasureTimelineItem(CreateNode(timeline, measuredNode)).Width),
                IsDraggedSlot = rawItems.Any(draggedItems.Contains),
                Left = 0
            });
        }

        UpdatePreviewSlotPositions(slots);
        return slots;
    }

    private void UpdatePreviewSlotPositions(IList<NodePreviewSlot> slots)
    {
        var currentLeft = TimelineFirstItemLeft;
        foreach (var slot in slots)
        {
            slot.Left = _stepDragPreviewContentLeftX + currentLeft;
            currentLeft += slot.Width + TimelineItemGap;
        }
    }

    private void SyncStepDragPreviewRawSteps()
    {
        _stepDragPreviewRawSteps.Clear();

        foreach (var slot in _stepDragPreviewSlots)
        {
            foreach (var rawItem in slot.RawItems)
            {
                if (!_stepDragPreviewRawSteps.Contains(rawItem))
                    _stepDragPreviewRawSteps.Add(rawItem);
            }
        }
    }

    private bool UpdateStepDragPreviewFromMouse(Point currentPoint)
    {
        if (_lastStepDragPreviewMousePoint.HasValue)
        {
            var last = _lastStepDragPreviewMousePoint.Value;
            var epsilon = DragUi.PreviewMouseMoveEpsilon;

            if (Math.Abs(currentPoint.X - last.X) < epsilon &&
                Math.Abs(currentPoint.Y - last.Y) < epsilon)
            {
                return false;
            }
        }

        _lastStepDragPreviewMousePoint = currentPoint;

        var orderChanged = UpdateStepDragPreviewOrderFromMouse(currentPoint.X);
        var rawInsertAnchor = GetStepDragPreviewRawInsertAnchor();
        var anchorChanged = _drag.UpdateStepDragPreview(currentPoint, rawInsertAnchor);

        return orderChanged || anchorChanged;
    }

    private bool UpdateStepDragPreviewOrderFromMouse(double mouseX)
    {
        if (_stepDragPreviewSlots.Count == 0 || _stepDragRawItems.Count == 0)
            return false;

        UpdatePreviewSlotPositions(_stepDragPreviewSlots);

        var draggedSlotIndex = FindDraggedDisplaySlotIndex(_stepDragPreviewSlots);
        if (draggedSlotIndex < 0)
            return false;

        if (ShouldMoveDraggedSlotLeft(_stepDragPreviewSlots, draggedSlotIndex, mouseX))
            return MoveSelectedPreviewSlotsLeft(_stepDragPreviewSlots);

        if (ShouldMoveDraggedSlotRight(_stepDragPreviewSlots, draggedSlotIndex, mouseX))
            return MoveSelectedPreviewSlotsRight(_stepDragPreviewSlots);

        return false;
    }

    private static bool ShouldMoveDraggedSlotLeft(IReadOnlyList<NodePreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex > 0 && mouseX < slots[draggedSlotIndex - 1].CenterX;
    }

    private static bool ShouldMoveDraggedSlotRight(IReadOnlyList<NodePreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex < slots.Count - 1 && mouseX > slots[draggedSlotIndex + 1].CenterX;
    }

    private int FindDraggedDisplaySlotIndex(IReadOnlyList<NodePreviewSlot> slots)
    {
        if (_drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
            return -1;

        var draggedRawItems = GetRawStepsForDisplayStep(_stepDragPreviewRawSteps, _drag.DraggedStep);
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].RawItems.Any(draggedRawItems.Contains))
                return i;
        }

        return -1;
    }

    private bool MoveSelectedPreviewSlotsLeft(IList<NodePreviewSlot> slots)
    {
        var changed = false;

        for (var i = 1; i < slots.Count; i++)
        {
            if (!slots[i].IsDraggedSlot || slots[i - 1].IsDraggedSlot)
                continue;

            (slots[i - 1], slots[i]) = (slots[i], slots[i - 1]);
            changed = true;
        }

        if (!changed)
            return false;

        UpdatePreviewSlotPositions(slots);
        SyncStepDragPreviewRawSteps();
        return true;
    }

    private bool MoveSelectedPreviewSlotsRight(IList<NodePreviewSlot> slots)
    {
        var changed = false;

        for (var i = slots.Count - 2; i >= 0; i--)
        {
            if (!slots[i].IsDraggedSlot || slots[i + 1].IsDraggedSlot)
                continue;

            (slots[i], slots[i + 1]) = (slots[i + 1], slots[i]);
            changed = true;
        }

        if (!changed)
            return false;

        UpdatePreviewSlotPositions(slots);
        SyncStepDragPreviewRawSteps();
        return true;
    }

    private MacroNode? GetStepDragPreviewRawInsertAnchor()
    {
        if (_stepDragPreviewSlots.Count == 0)
            return null;

        var lastDraggedSlotIndex = -1;
        for (var i = 0; i < _stepDragPreviewSlots.Count; i++)
        {
            if (_stepDragPreviewSlots[i].IsDraggedSlot)
                lastDraggedSlotIndex = i;
        }

        if (lastDraggedSlotIndex < 0)
            return null;

        for (var i = lastDraggedSlotIndex + 1; i < _stepDragPreviewSlots.Count; i++)
        {
            var nextRawItem = _stepDragPreviewSlots[i].RawItems.FirstOrDefault(rawItem => !_stepDragRawItems.Contains(rawItem));
            if (nextRawItem != null)
                return nextRawItem;
        }

        return null;
    }

    private IReadOnlyList<MacroNode> GetTimelineRenderRawSteps(MacroTimeline timeline)
    {
        if (_drag.IsDraggingStep &&
            ReferenceEquals(_drag.DraggedStepTimeline, timeline) &&
            _stepDragPreviewRawSteps.Count > 0)
        {
            return _stepDragPreviewRawSteps;
        }

        return timeline.Steps;
    }

    private IReadOnlyList<NodePreviewSlot> GetTimelineRenderPreviewSlots(MacroTimeline timeline)
    {
        if (_drag.IsDraggingStep &&
            ReferenceEquals(_drag.DraggedStepTimeline, timeline) &&
            _stepDragPreviewSlots.Count > 0)
        {
            return _stepDragPreviewSlots;
        }

        return Array.Empty<NodePreviewSlot>();
    }

    private double GetTimelineRowTopY(MacroTimeline timeline)
    {
        var index = _document.Timelines.IndexOf(timeline);
        if (index < 0)
            return 0;

        return index * (TimelineRowHeight + TimelineRowGap);
    }
}
