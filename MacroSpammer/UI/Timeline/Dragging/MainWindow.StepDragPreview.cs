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
        _stepDragPreviewWidthByFirstRawItem.Clear();
        _timelineVisualPositions.Remove(GetDropPlaceholderAnimationKey(timeline));

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

        var changed = false;
        var maxMoves = Math.Max(1, _stepDragPreviewRawSteps.Count);

        for (var move = 0; move < maxMoves; move++)
        {
            var slots = BuildStepPreviewSlots(_drag.DraggedStepTimeline);
            var draggedSlotIndex = slots.FindIndex(slot => slot.IsDraggedSlot);

            if (draggedSlotIndex < 0)
                break;

            if (ShouldMoveDraggedSlotLeft(slots, draggedSlotIndex, mouseX))
            {
                MoveDraggedPreviewBeforeRawAnchor(slots[draggedSlotIndex - 1].RawItems[0]);
                changed = true;
                continue;
            }

            if (ShouldMoveDraggedSlotRight(slots, draggedSlotIndex, mouseX))
            {
                var anchorAfterRightNeighbor = GetAnchorAfterRightNeighbor(slots, draggedSlotIndex);

                MoveDraggedPreviewBeforeRawAnchor(anchorAfterRightNeighbor);
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

    private static MacroStep? GetAnchorAfterRightNeighbor(IReadOnlyList<StepPreviewSlot> slots, int draggedSlotIndex)
    {
        return draggedSlotIndex + 2 < slots.Count
            ? slots[draggedSlotIndex + 2].RawItems[0]
            : null;
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
}
