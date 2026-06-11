using KeyLine.Domain;
using KeyLine.State;

namespace KeyLine.UI.Inspector;

public sealed class InspectorCommitService
{
    private readonly TimelineSelectionState _selection;
    private readonly Func<bool> _canEdit;
    private readonly Action _saveUndoSnapshot;
    private readonly InspectorTimelineRefresh _refresh;
    private readonly Action _refreshInspector;
    private readonly Action _scheduleSaveState;
    private readonly Action<MacroTimeline> _selectTimeline;

    private bool _isCommittingTimelineName;

    public InspectorCommitService(
        TimelineSelectionState selection,
        Func<bool> canEdit,
        Action saveDocumentUndoSnapshot,
        InspectorTimelineRefresh refresh,
        Action refreshInspector,
        Action scheduleSaveState,
        Action<MacroTimeline> selectTimeline)
    {
        _selection = selection;
        _canEdit = canEdit;
        _saveUndoSnapshot = saveDocumentUndoSnapshot;
        _refresh = refresh;
        _refreshInspector = refreshInspector;
        _scheduleSaveState = scheduleSaveState;
        _selectTimeline = selectTimeline;
    }

    // Node edit that may change the node's display/structure: rebuild only the selected timeline's
    // row, then reload the inspector. (Batch node edits mutate every selected node inside this one
    // call — all in the same timeline — so the single row refresh covers them once.)
    public void CommitNodeChange(Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        RefreshSelectedNodeTimeline();
        _refreshInspector();
        _scheduleSaveState();
    }

    // Live node value edit that must not reload the inspector (so the edited field keeps focus).
    public void CommitNodeValueChange(Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        RefreshSelectedNodeTimeline();
        _scheduleSaveState();
    }

    // Node edits all live within a single timeline (node selection is single-timeline), and batch
    // node edits mutate every selected node in this one call — so rebuilding just that timeline's
    // row refreshes them all once.
    private void RefreshSelectedNodeTimeline()
    {
        var timeline = _selection.SelectedTimeline;
        if (timeline == null)
            _refresh.Full();
        else
            _refresh.Row(timeline);
    }

    // Timeline option that may change how its nodes render (key up/down, standard-delay toggle,
    // text-input mode): rebuild that row + re-skin headers, and reload the inspector (its option
    // layout can change). LoopCount also routes here; the extra single-row rebuild is cheap.
    public void CommitTimelineChange(MacroTimeline timeline, Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _selectTimeline(timeline);
        _refresh.Row(timeline);
        _refresh.HeaderStates();
        _refreshInspector();
        _scheduleSaveState();
    }

    // Timeline header-meta value (loop delay, cooldown, standard-delay value): re-skin the header
    // only — node visuals and inspector layout are unaffected, so neither is rebuilt.
    public void CommitTimelineValueChange(MacroTimeline timeline, Action change)
    {
        if (!_canEdit())
            return;

        _saveUndoSnapshot();
        change();
        _selection.SelectTimeline(timeline);
        _refresh.HeaderStates();
        _scheduleSaveState();
    }

    // Batch header-meta edit (loop count / delay / cooldown / standard-delay value across selected
    // timelines): one undo step, header re-skin once. No node rows are rebuilt.
    public void CommitTimelinesHeaderChange(IReadOnlyList<MacroTimeline> timelines, Action<MacroTimeline> change)
    {
        if (!_canEdit() || timelines.Count == 0)
            return;

        _saveUndoSnapshot();
        foreach (var timeline in timelines)
            change(timeline);

        _refresh.HeaderStates();
        _refreshInspector();
        _scheduleSaveState();
    }

    // Batch display-option edit (key up/down, standard-delay toggle, text-input mode across selected
    // timelines): one undo step, rebuild each affected row once + header re-skin once.
    public void CommitTimelinesDisplayChange(IReadOnlyList<MacroTimeline> timelines, Action<MacroTimeline> change)
    {
        if (!_canEdit() || timelines.Count == 0)
            return;

        _saveUndoSnapshot();
        foreach (var timeline in timelines)
            change(timeline);

        foreach (var timeline in timelines)
            _refresh.Row(timeline);

        _refresh.HeaderStates();
        _refreshInspector();
        _scheduleSaveState();
    }

    public bool CommitTimelineName(MacroTimeline timeline, string name)
    {
        if (_isCommittingTimelineName || !_canEdit())
            return false;

        var trimmedName = string.IsNullOrWhiteSpace(name) ? timeline.Name : name.Trim();
        if (string.Equals(timeline.Name, trimmedName, StringComparison.Ordinal))
            return false;

        _isCommittingTimelineName = true;
        try
        {
            _saveUndoSnapshot();
            timeline.Name = trimmedName;
            _selection.SelectTimeline(timeline);
            _refresh.HeaderStates();
            _refreshInspector();
            _scheduleSaveState();
            return true;
        }
        finally
        {
            _isCommittingTimelineName = false;
        }
    }
}
