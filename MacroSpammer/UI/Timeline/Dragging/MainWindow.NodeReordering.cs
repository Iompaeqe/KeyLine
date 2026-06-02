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

        if (_selection.HasMultipleNodeSelection &&
            _selection.IsNodeSelected(timeline, draggedNode) &&
            TryApplyStepDragPreviewOrder(timeline, draggedNode))
        {
            return;
        }

        if (rawInsertAnchor != null && draggedItems.Contains(rawInsertAnchor))
            return;

        var oldFirstIndex = timeline.Nodes.IndexOf(draggedItems[0]);

        var newIndexBeforeRemoval = rawInsertAnchor == null
            ? timeline.Nodes.Count
            : timeline.Nodes.IndexOf(rawInsertAnchor);

        if (newIndexBeforeRemoval < 0)
            newIndexBeforeRemoval = timeline.Nodes.Count;

        if (IsSameDragPosition(oldFirstIndex, draggedItems.Count, newIndexBeforeRemoval))
            return;

        RemoveDraggedItems(timeline, draggedItems);

        var insertIndex = rawInsertAnchor == null
            ? timeline.Nodes.Count
            : timeline.Nodes.IndexOf(rawInsertAnchor);

        if (insertIndex < 0)
            insertIndex = timeline.Nodes.Count;

        InsertDraggedItems(timeline, insertIndex, draggedItems);

        if (_selection.IsNodeSelected(timeline, draggedNode))
            _selection.SelectNodes(
                timeline,
                _selection.SelectedNodes.Where(step => timeline.Nodes.Contains(step)).ToList(),
                _selection.AnchorNode);
        else
            _selection.SelectNode(timeline, draggedNode);
    }

    private bool TryApplyStepDragPreviewOrder(MacroTimeline timeline, MacroNode draggedNode)
    {
        if (_stepDragPreviewRawSteps.Count != timeline.Nodes.Count ||
            _stepDragPreviewRawSteps.Any(step => !timeline.Nodes.Contains(step)))
        {
            return false;
        }

        if (_stepDragPreviewRawSteps.SequenceEqual(timeline.Nodes))
            return true;

        timeline.Nodes.Clear();
        foreach (var step in _stepDragPreviewRawSteps)
            timeline.Nodes.Add(step);

        _selection.SelectNodes(
            timeline,
            _selection.SelectedNodes.Where(step => timeline.Nodes.Contains(step)).ToList(),
            _selection.AnchorNode);
        if (!_selection.HasNodeSelection)
            _selection.SelectNode(timeline, draggedNode);

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
            timeline.Nodes.Remove(item);
    }

    private static void InsertDraggedItems(MacroTimeline timeline, int insertIndex, IReadOnlyList<MacroNode> draggedItems)
    {
        for (var i = 0; i < draggedItems.Count; i++)
            timeline.Nodes.Insert(insertIndex + i, draggedItems[i]);
    }

    private List<MacroNode> GetRawStepsForDrag(MacroTimeline timeline, MacroNode draggedNode)
    {
        return _selection.IsNodeSelected(timeline, draggedNode)
            ? GetSelectedRawSteps(timeline)
            : GetRawStepsForDisplayStep(timeline, draggedNode);
    }

    private static List<MacroNode> GetRawStepsForDisplayStep(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(sourceStep => timeline.Nodes.Contains(sourceStep))
                .ToList();
        }

        if (!timeline.Nodes.Contains(node))
            return new List<MacroNode>();

        var rawSteps = new List<MacroNode> { node };
        AddAttachedStandardDelaySteps(timeline, node, rawSteps);
        return rawSteps
            .Distinct()
            .OrderBy(rawStep => timeline.Nodes.IndexOf(rawStep))
            .ToList();
    }

    private static void AddAttachedStandardDelaySteps(MacroTimeline timeline, MacroNode node, List<MacroNode> rawSteps)
    {
        if (!timeline.UseStandardDelay || node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay)
            return;

        var stepIndex = timeline.Nodes.IndexOf(node);
        if (stepIndex < 0)
            return;

        var delaySteps = GetContiguousDelayStepsBefore(timeline, stepIndex);
        if (delaySteps.Count == 0)
            delaySteps = GetContiguousDelayStepsAfter(timeline, stepIndex);

        foreach (var delayStep in delaySteps)
            rawSteps.Add(delayStep);
    }
}
