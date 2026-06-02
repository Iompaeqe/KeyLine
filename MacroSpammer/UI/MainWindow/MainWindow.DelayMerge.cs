using MacroSpammer.Domain;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private bool MergeAdjacentDelayNodesIfEnabled(MacroTimeline timeline)
    {
        if (!_settings.MergeRepeatedDelayNodes)
            return false;

        var changed = TimelineNodeMutationService.MergeAdjacentDelayNodes(timeline);
        if (changed)
            PruneSelectionAfterDelayMerge(timeline);

        return changed;
    }


    private void PruneSelectionAfterDelayMerge(MacroTimeline timeline)
    {
        if (!ReferenceEquals(_selection.SelectedTimeline, timeline) || !_selection.HasNodeSelection)
            return;

        var selectedSteps = _selection.SelectedNodes
            .Where(timeline.Nodes.Contains)
            .ToList();

        if (selectedSteps.Count == 0)
        {
            _selection.Clear();
            return;
        }

        var currentAnchor = _selection.AnchorNode;
        var anchorStep = currentAnchor != null && timeline.Nodes.Contains(currentAnchor)
            ? currentAnchor
            : selectedSteps[^1];

        _selection.SelectNodes(timeline, selectedSteps, anchorStep);
    }
}