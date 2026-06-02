using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private bool MergeAdjacentDelayNodesIfEnabled(MacroTimeline timeline)
    {
        if (!_settings.MergeRepeatedDelayNodes)
            return false;

        var changed = MergeAdjacentDelayNodes(timeline);
        if (changed)
            PruneSelectionAfterDelayMerge(timeline);

        return changed;
    }

    private static bool MergeAdjacentDelayNodes(MacroTimeline timeline)
    {
        var changed = false;

        for (var i = 1; i < timeline.Nodes.Count; i++)
        {
            var previous = timeline.Nodes[i - 1];
            var current = timeline.Nodes[i];

            if (previous.Type != MacroNodeType.Delay || current.Type != MacroNodeType.Delay)
                continue;

            previous.DelayMs += current.DelayMs;
            previous.IsRecordedDelay = previous.IsRecordedDelay && current.IsRecordedDelay;
            timeline.Nodes.RemoveAt(i);
            changed = true;
            i--;
        }

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
