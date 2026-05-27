using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Timeline;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private void BeginStepDragPreviewModel(MacroTimeline timeline, MacroStep draggedStep)
    {
        _stepDragPreviewRawSteps.Clear();
        _stepDragRawItems.Clear();
        _stepDragOriginalRawSteps.Clear();
        _stepDragPreviewWidthByFirstRawItem.Clear();
        _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(timeline));

        var draggedItems = GetRawStepsForDrag(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        _stepDragRawItems.AddRange(draggedItems);
        _stepDragOriginalRawSteps.AddRange(timeline.Steps);
        _stepDragPreviewRawSteps.AddRange(timeline.Steps);
        _stepDragSlotWidth = GetDraggedStepSlotWidth(timeline, draggedStep);

        BuildStepDragPreviewLayoutCache(timeline);

        _drag.UpdateStepDragPreview(_drag.StepDragCurrentPoint, GetStepDragPreviewRawInsertAnchor());
    }

    private void BuildStepDragPreviewLayoutCache(MacroTimeline timeline)
    {
        _stepDragPreviewWidthByFirstRawItem.Clear();

        // TimelineRowsPanel lives inside the node/content column.
        // Mouse coordinates used for step dragging are already relative to that column,
        // so there is no header-column offset here.
        _stepDragPreviewContentLeftX = 0;

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _stepDragPreviewRawSteps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        foreach (var displayStep in visibleSteps)
        {
            var rawItems = GetRawStepsForPreviewDisplayStep(timeline, displayStep);
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

    private double GetDraggedStepSlotWidth(MacroTimeline timeline, MacroStep draggedStep)
    {
        var block = CreateStepBlock(timeline, draggedStep);
        var size = MeasureTimelineItem(block);
        return Math.Max(1, size.Width + TimelineItemGap);
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
        if (_drag.DraggedStepTimeline == null || _stepDragPreviewRawSteps.Count == 0 || _stepDragRawItems.Count == 0)
            return false;

        if (_selection.HasMultipleStepSelection && _drag.DraggedStep != null &&
            _selection.IsStepSelected(_drag.DraggedStepTimeline, _drag.DraggedStep))
        {
            return UpdateMultiStepDragPreviewOrderFromMouse(_drag.DraggedStepTimeline, mouseX);
        }

        var changed = false;
        var maxMoves = _selection.HasMultipleStepSelection ? 1 : Math.Max(1, _stepDragPreviewRawSteps.Count);

        for (var move = 0; move < maxMoves; move++)
        {
            var slots = BuildStepPreviewSlots(_drag.DraggedStepTimeline);
            var draggedSlotIndex = FindDraggedDisplaySlotIndex(slots);

            if (draggedSlotIndex < 0)
                break;

            if (ShouldMoveDraggedSlotLeft(slots, draggedSlotIndex, mouseX))
            {
                if (!MoveSelectedPreviewSlotsLeft(slots))
                    break;

                changed = true;
                continue;
            }

            if (ShouldMoveDraggedSlotRight(slots, draggedSlotIndex, mouseX))
            {
                if (!MoveSelectedPreviewSlotsRight(slots))
                    break;

                changed = true;
                continue;
            }

            break;
        }

        return changed;
    }

    private static bool ShouldMoveDraggedSlotLeft(IReadOnlyList<StepPreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex > 0 && mouseX < slots[draggedSlotIndex - 1].CenterX;
    }

    private static bool ShouldMoveDraggedSlotRight(IReadOnlyList<StepPreviewSlot> slots, int draggedSlotIndex, double mouseX)
    {
        return draggedSlotIndex < slots.Count - 1 && mouseX > slots[draggedSlotIndex + 1].CenterX;
    }

    private int FindDraggedDisplaySlotIndex(IReadOnlyList<StepPreviewSlot> slots)
    {
        if (_drag.DraggedStep == null || _drag.DraggedStepTimeline == null)
            return -1;

        var draggedRawItems = GetRawStepsForPreviewDisplayStep(_drag.DraggedStepTimeline, _drag.DraggedStep);
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i].RawItems.Any(draggedRawItems.Contains))
                return i;
        }

        return -1;
    }

    private bool MoveSelectedPreviewSlotsLeft(IReadOnlyList<StepPreviewSlot> slots)
    {
        var changed = false;

        for (var i = 1; i < slots.Count; i++)
        {
            if (!slots[i].IsDraggedSlot || slots[i - 1].IsDraggedSlot)
                continue;

            if (SwapPreviewSlotGroups(slots[i - 1], slots[i]))
                changed = true;
        }

        return changed;
    }

    private bool MoveSelectedPreviewSlotsRight(IReadOnlyList<StepPreviewSlot> slots)
    {
        var changed = false;

        for (var i = slots.Count - 2; i >= 0; i--)
        {
            if (!slots[i].IsDraggedSlot || slots[i + 1].IsDraggedSlot)
                continue;

            if (SwapPreviewSlotGroups(slots[i], slots[i + 1]))
                changed = true;
        }

        return changed;
    }

    private bool SwapPreviewSlotGroups(StepPreviewSlot leftSlot, StepPreviewSlot rightSlot)
    {
        if (leftSlot.RawItems.Count == 0 || rightSlot.RawItems.Count == 0)
            return false;

        var leftIndex = _stepDragPreviewRawSteps.IndexOf(leftSlot.RawItems[0]);
        var rightIndex = _stepDragPreviewRawSteps.IndexOf(rightSlot.RawItems[0]);
        if (leftIndex < 0 || rightIndex < 0 || leftIndex >= rightIndex)
            return false;

        foreach (var item in rightSlot.RawItems)
            _stepDragPreviewRawSteps.Remove(item);

        foreach (var item in leftSlot.RawItems)
            _stepDragPreviewRawSteps.Remove(item);

        leftIndex = Math.Min(leftIndex, _stepDragPreviewRawSteps.Count);

        for (var i = 0; i < rightSlot.RawItems.Count; i++)
            _stepDragPreviewRawSteps.Insert(leftIndex + i, rightSlot.RawItems[i]);

        var leftInsertIndex = leftIndex + rightSlot.RawItems.Count;
        for (var i = 0; i < leftSlot.RawItems.Count; i++)
            _stepDragPreviewRawSteps.Insert(leftInsertIndex + i, leftSlot.RawItems[i]);

        return true;
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
            var rawItems = GetRawStepsForPreviewDisplayStep(timeline, displayStep);
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

    private MacroStep? GetStepDragPreviewRawInsertAnchor()
    {
        if (_drag.DraggedStep == null || _drag.DraggedStepTimeline == null || _stepDragPreviewRawSteps.Count == 0)
            return null;

        var draggedItems = GetRawStepsForPreviewDisplayStep(_drag.DraggedStepTimeline, _drag.DraggedStep);
        if (draggedItems.Count == 0)
            return null;

        var draggedFirstIndex = _stepDragPreviewRawSteps.IndexOf(draggedItems[0]);
        if (draggedFirstIndex < 0)
            return null;

        var afterDraggedIndex = draggedFirstIndex + draggedItems.Count;

        for (var i = afterDraggedIndex; i < _stepDragPreviewRawSteps.Count; i++)
        {
            var candidate = _stepDragPreviewRawSteps[i];
            if (!draggedItems.Contains(candidate))
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

    private double GetTimelineRowTopY(MacroTimeline timeline)
    {
        var index = _document.Timelines.IndexOf(timeline);
        if (index < 0)
            return 0;

        return index * (TimelineRowHeight + TimelineRowGap);
    }
}
