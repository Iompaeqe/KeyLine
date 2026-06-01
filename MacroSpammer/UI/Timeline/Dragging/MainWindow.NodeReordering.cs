using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void MoveStepBeforeRawAnchor(
        MacroTimeline timeline,
        MacroNode draggedNode,
        MacroNode? rawInsertAnchor)
    {
        var draggedItems = GetRawStepsForDrag(timeline, draggedNode);
        if (draggedItems.Count == 0)
            return;

        if (_selection.HasMultipleStepSelection &&
            _selection.IsStepSelected(timeline, draggedNode) &&
            TryApplyStepDragPreviewOrder(timeline, draggedNode))
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

        if (_selection.IsStepSelected(timeline, draggedNode))
            _selection.SelectSteps(
                timeline,
                _selection.SelectedSteps.Where(step => timeline.Steps.Contains(step)).ToList(),
                _selection.AnchorStep);
        else
            _selection.SelectStep(timeline, draggedNode);
    }

    private bool TryApplyStepDragPreviewOrder(MacroTimeline timeline, MacroNode draggedNode)
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
            _selection.SelectStep(timeline, draggedNode);

        return true;
    }

    private static bool IsSameDragPosition(int oldFirstIndex, int draggedItemCount, int newIndexBeforeRemoval)
    {
        return newIndexBeforeRemoval == oldFirstIndex ||
               newIndexBeforeRemoval == oldFirstIndex + draggedItemCount;
    }

    private static void RemoveDraggedItems(MacroTimeline timeline, IEnumerable<MacroNode> draggedItems)
    {
        foreach (var item in draggedItems)
            timeline.Steps.Remove(item);
    }

    private static void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroNode> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Steps.Insert(insertIndex + i, draggedItems[i]);
    }

    private List<MacroNode> GetRawStepsForDrag(MacroTimeline timeline, MacroNode draggedNode)
    {
        return _selection.IsStepSelected(timeline, draggedNode)
            ? GetSelectedRawSteps(timeline)
            : GetRawStepsForDisplayStep(timeline, draggedNode);
    }

    private static List<MacroNode> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(sourceStep => timeline.Steps.Contains(sourceStep))
                .ToList();
        }

        if (!timeline.Steps.Contains(node))
            return new List<MacroNode>();

        var rawSteps = new List<MacroNode> { node };
        AddAttachedStandardDelaySteps(timeline, node, rawSteps);
        return rawSteps
            .Distinct()
            .OrderBy(rawStep => timeline.Steps.IndexOf(rawStep))
            .ToList();
    }

    private static void AddAttachedStandardDelaySteps(MacroTimeline timeline, MacroNode node, List<MacroNode> rawSteps)
    {
        if (!timeline.UseStandardDelay || node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay)
            return;

        var stepIndex = timeline.Steps.IndexOf(node);
        if (stepIndex < 0)
            return;

        var delaySteps = GetContiguousDelayStepsBefore(timeline, stepIndex);
        if (delaySteps.Count == 0)
            delaySteps = GetContiguousDelayStepsAfter(timeline, stepIndex);

        foreach (var delayStep in delaySteps)
            rawSteps.Add(delayStep);
    }
}
