using System.Collections.Generic;
using System.Linq;
using MacroSpammer.Domain;
using MacroSpammer.Services.Timeline;

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

        var changed = TimelineNodeMutationService.MoveRawStepsBeforeAnchor(
            timeline,
            draggedItems,
            rawInsertAnchor);

        if (!changed)
            return;

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
        if (_nodeDragPreview.PreviewRawSteps.Count != timeline.Nodes.Count ||
            _nodeDragPreview.PreviewRawSteps.Any(step => !timeline.Nodes.Contains(step)))
        {
            return false;
        }

        if (_nodeDragPreview.PreviewRawSteps.SequenceEqual(timeline.Nodes))
            return true;

        timeline.Nodes.Clear();
        foreach (var step in _nodeDragPreview.PreviewRawSteps)
            timeline.Nodes.Add(step);

        _selection.SelectNodes(
            timeline,
            _selection.SelectedNodes.Where(step => timeline.Nodes.Contains(step)).ToList(),
            _selection.AnchorNode);
        if (!_selection.HasNodeSelection)
            _selection.SelectNode(timeline, draggedNode);

        return true;
    }

    private List<MacroNode> GetRawStepsForDrag(MacroTimeline timeline, MacroNode draggedNode)
    {
        return _selection.IsNodeSelected(timeline, draggedNode)
            ? GetSelectedRawSteps(timeline)
            : TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, draggedNode);
    }
}
