using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void MoveStepBeforeRawAnchor(
        MacroTimeline timeline,
        MacroStep draggedStep,
        MacroStep? rawInsertAnchor)
    {
        var draggedItems = GetRawStepsForDrag(timeline, draggedStep);
        if (draggedItems.Count == 0)
            return;

        if (_selection.HasMultipleStepSelection &&
            _selection.IsStepSelected(timeline, draggedStep) &&
            TryApplyStepDragPreviewOrder(timeline, draggedStep))
        {
            return;
        }

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

        if (_selection.IsStepSelected(timeline, draggedStep))
            _selection.SelectSteps(
                timeline,
                _selection.SelectedSteps.Where(step => timeline.Steps.Contains(step)).ToList(),
                _selection.AnchorStep);
        else
            _selection.SelectStep(timeline, draggedStep);
    }

    private bool TryApplyStepDragPreviewOrder(MacroTimeline timeline, MacroStep draggedStep)
    {
        if (_stepDragPreviewRawSteps.Count != timeline.Steps.Count ||
            _stepDragPreviewRawSteps.Any(step => !timeline.Steps.Contains(step)))
        {
            return false;
        }

        if (_stepDragPreviewRawSteps.SequenceEqual(timeline.Steps))
            return true;

        timeline.Steps.Clear();
        foreach (var step in _stepDragPreviewRawSteps)
            timeline.Steps.Add(step);

        _selection.SelectSteps(
            timeline,
            _selection.SelectedSteps.Where(step => timeline.Steps.Contains(step)).ToList(),
            _selection.AnchorStep);
        if (!_selection.HasStepSelection)
            _selection.SelectStep(timeline, draggedStep);

        return true;
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private static void RemoveDraggedItems(MacroTimeline timeline, IEnumerable<MacroStep> draggedItems)
    {
        foreach (var item in draggedItems)
            timeline.Steps.Remove(item);
    }

    private static void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroStep> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Steps.Insert(insertIndex + i, draggedItems[i]);
    }

    private List<MacroStep> GetRawStepsForDrag(MacroTimeline timeline, MacroStep draggedStep)
    {
        return _selection.IsStepSelected(timeline, draggedStep)
            ? GetSelectedRawSteps(timeline)
            : GetRawStepsForDisplayStep(timeline, draggedStep);
    }

    private static List<MacroStep> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroStep step)
    {
        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps
                .Where(sourceStep => timeline.Steps.Contains(sourceStep))
                .ToList();
        }

        if (!timeline.Steps.Contains(step))
            return new List<MacroStep>();

        var rawSteps = new List<MacroStep> { step };
        AddAttachedStandardDelaySteps(timeline, step, rawSteps);
        return rawSteps
            .Distinct()
            .OrderBy(rawStep => timeline.Steps.IndexOf(rawStep))
            .ToList();
    }

    private static void AddAttachedStandardDelaySteps(MacroTimeline timeline, MacroStep step, List<MacroStep> rawSteps)
    {
        if (!timeline.UseStandardDelay || step.Type is MacroStepType.Delay or MacroStepType.RandomDelay)
            return;

        var stepIndex = timeline.Steps.IndexOf(step);
        if (stepIndex < 0)
            return;

        var delaySteps = GetContiguousDelayStepsBefore(timeline, stepIndex);
        if (delaySteps.Count == 0)
            delaySteps = GetContiguousDelayStepsAfter(timeline, stepIndex);

        foreach (var delayStep in delaySteps)
            rawSteps.Add(delayStep);
    }
}
