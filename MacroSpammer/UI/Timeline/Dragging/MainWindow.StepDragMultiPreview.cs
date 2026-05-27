using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private bool UpdateMultiStepDragPreviewOrderFromMouse(MacroTimeline timeline, double mouseX)
    {
        if (_stepDragOriginalRawSteps.Count == 0 || _stepDragSlotWidth <= 0)
            return false;

        var slotOffset = GetDraggedSlotOffset(mouseX);
        var reorderedSteps = BuildMultiDragPreviewRawSteps(timeline, slotOffset);

        if (reorderedSteps.SequenceEqual(_stepDragPreviewRawSteps))
            return false;

        _stepDragPreviewRawSteps.Clear();
        _stepDragPreviewRawSteps.AddRange(reorderedSteps);
        return true;
    }

    private int GetDraggedSlotOffset(double mouseX)
    {
        var deltaX = mouseX - _drag.StepDragStartPoint.X;
        return deltaX >= 0
            ? (int)Math.Floor(deltaX / _stepDragSlotWidth)
            : (int)Math.Ceiling(deltaX / _stepDragSlotWidth);
    }

    private List<MacroStep> BuildMultiDragPreviewRawSteps(MacroTimeline timeline, int slotOffset)
    {
        var slots = BuildOriginalStepSlots(timeline);
        if (slots.Count == 0 || slotOffset == 0)
            return RestoreMissingOriginalRawSteps(FlattenStepSlots(slots));

        var selectedIndexes = slots
            .Select((slot, index) => new { slot, index })
            .Where(item => item.slot.IsDraggedSlot)
            .Select(item => item.index)
            .ToList();

        if (selectedIndexes.Count == 0)
            return RestoreMissingOriginalRawSteps(FlattenStepSlots(slots));

        var minOffset = -selectedIndexes[0];
        var maxOffset = slots.Count - 1 - selectedIndexes[^1];
        var clampedOffset = Math.Clamp(slotOffset, minOffset, maxOffset);
        if (clampedOffset == 0)
            return RestoreMissingOriginalRawSteps(FlattenStepSlots(slots));

        var targetSlots = new StepPreviewSlot?[slots.Count];
        foreach (var index in selectedIndexes)
            targetSlots[index + clampedOffset] = slots[index];

        var unselectedSlots = slots.Where(slot => !slot.IsDraggedSlot).ToList();
        var unselectedIndex = 0;
        for (var i = 0; i < targetSlots.Length; i++)
        {
            if (targetSlots[i] == null)
                targetSlots[i] = unselectedSlots[unselectedIndex++];
        }

        return RestoreMissingOriginalRawSteps(FlattenStepSlots(targetSlots.Select(slot => slot!)));
    }

    private List<StepPreviewSlot> BuildOriginalStepSlots(MacroTimeline timeline)
    {
        var slots = new List<StepPreviewSlot>();
        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _stepDragOriginalRawSteps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        foreach (var displayStep in visibleSteps)
        {
            var rawItems = GetRawStepsForOriginalDisplayStep(timeline, displayStep);
            if (rawItems.Count == 0)
                continue;

            slots.Add(new StepPreviewSlot
            {
                RawItems = rawItems,
                CenterX = 0,
                IsDraggedSlot = IsStepSelected(timeline, displayStep)
            });
        }

        return slots;
    }

    private static List<MacroStep> FlattenStepSlots(IEnumerable<StepPreviewSlot> slots)
    {
        return slots
            .SelectMany(slot => slot.RawItems)
            .Distinct()
            .ToList();
    }

    private List<MacroStep> RestoreMissingOriginalRawSteps(List<MacroStep> reorderedSteps)
    {
        foreach (var originalStep in _stepDragOriginalRawSteps)
        {
            if (reorderedSteps.Contains(originalStep))
                continue;

            var originalIndex = _stepDragOriginalRawSteps.IndexOf(originalStep);
            var insertIndex = 0;

            for (var i = originalIndex - 1; i >= 0; i--)
            {
                var previousOriginal = _stepDragOriginalRawSteps[i];
                var previousIndex = reorderedSteps.IndexOf(previousOriginal);
                if (previousIndex >= 0)
                {
                    insertIndex = previousIndex + 1;
                    break;
                }
            }

            reorderedSteps.Insert(Math.Clamp(insertIndex, 0, reorderedSteps.Count), originalStep);
        }

        return reorderedSteps;
    }
}
