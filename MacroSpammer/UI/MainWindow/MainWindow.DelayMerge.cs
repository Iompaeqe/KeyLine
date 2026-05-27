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

        for (var i = 1; i < timeline.Steps.Count; i++)
        {
            var previous = timeline.Steps[i - 1];
            var current = timeline.Steps[i];

            if (previous.Type != MacroStepType.Delay || current.Type != MacroStepType.Delay)
                continue;

            previous.DelayMs += current.DelayMs;
            previous.IsRecordedDelay = previous.IsRecordedDelay && current.IsRecordedDelay;
            timeline.Steps.RemoveAt(i);
            changed = true;
            i--;
        }

        return changed;
    }

    private void PruneSelectionAfterDelayMerge(MacroTimeline timeline)
    {
        if (!ReferenceEquals(_selection.SelectedTimeline, timeline) || !_selection.HasStepSelection)
            return;

        var selectedSteps = _selection.SelectedSteps
            .Where(timeline.Steps.Contains)
            .ToList();

        if (selectedSteps.Count == 0)
        {
            _selection.Clear();
            return;
        }

        var currentAnchor = _selection.AnchorStep;
        var anchorStep = currentAnchor != null && timeline.Steps.Contains(currentAnchor)
            ? currentAnchor
            : selectedSteps[^1];

        _selection.SelectSteps(timeline, selectedSteps, anchorStep);
    }
}
