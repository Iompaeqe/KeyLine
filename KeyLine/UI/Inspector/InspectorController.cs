using System.Windows;
using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector;

public sealed class InspectorController
{
    private readonly InspectorWindow _window;
    private readonly TimelineSelectionState _selection;
    private readonly Func<MacroTimeline> _getCurrentTimeline;
    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getActiveProfileWorkspaces;
    private readonly Func<bool> _canEdit;
    private readonly InspectorCommitService _commitService;
    private readonly NodeInspectorBuilder _nodeInspectorBuilder;

    private bool _isRefreshing;
    private MacroTimeline? _editingTimelineName;
    private bool _isTimelineCollapsed;
    private bool _isNodeCollapsed;

    public InspectorController(
        InspectorWindow window,
        TimelineSelectionState selection,
        Func<MacroTimeline> getCurrentTimeline,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<bool> canEdit,
        Action saveDocumentUndoSnapshot,
        Action refreshTimeline,
        Action refreshTimelineWithoutInspector,
        Action scheduleSaveState,
        Action<MacroTimeline> selectTimeline,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync,
        Func<MacroNode, Task> pickConditionPixelAsync)
    {
        _window = window;
        _selection = selection;
        _getCurrentTimeline = getCurrentTimeline;
        _getActiveWorkspace = getActiveWorkspace;
        _getActiveProfileWorkspaces = getActiveProfileWorkspaces;
        _canEdit = canEdit;

        _commitService = new InspectorCommitService(
            selection: selection,
            canEdit: canEdit,
            saveDocumentUndoSnapshot: saveDocumentUndoSnapshot,
            refreshTimeline: refreshTimeline,
            refreshTimelineWithoutInspector: refreshTimelineWithoutInspector,
            refreshInspector: Refresh,
            scheduleSaveState: scheduleSaveState,
            selectTimeline: selectTimeline);

        _nodeInspectorBuilder = new NodeInspectorBuilder(
            selection: selection,
            getActiveWorkspace: _getActiveWorkspace,
            getActiveProfileWorkspaces: _getActiveProfileWorkspaces,
            canEdit: canEdit,
            isRefreshing: () => _isRefreshing,
            saveDocumentUndoSnapshot: saveDocumentUndoSnapshot,
            commitNodeChange: _commitService.CommitNodeChange,
            commitNodeValueChange: _commitService.CommitNodeValueChange,
            refreshInspector: Refresh,
            pickMouseCoordinatesForNodeAsync: pickMouseCoordinatesForNodeAsync,
            pickConditionPixelAsync: pickConditionPixelAsync);

        WireWindowEvents();
    }

    public void Refresh()
    {
        _isRefreshing = true;
        try
        {
            var timeline = _getCurrentTimeline();
            var workspace = _getActiveWorkspace();
            var showCooldown =
                workspace.LoopMode is MacroLoopMode.Sequence &&
                !workspace.IsHookTimeline(timeline);

            if (TryGetBatchTimelines(out var batch))
            {
                var rep = batch.Contains(timeline) ? timeline : batch[0];

                _window.SetTimelineState(new TimelineInspectorState(
                    TimelineName: $"{batch.Count} timelines",
                    IsNameEditing: false,
                    IsCollapsed: _isTimelineCollapsed,
                    IsEditingEnabled: _canEdit(),
                    LoopCount: Math.Max(0, rep.LoopCount),
                    LoopDelayMs: Math.Max(0, rep.BaseDelayMs),
                    UseTextInputMode: rep.UseTextInputMode,
                    UseStandardDelay: rep.UseStandardDelay,
                    StandardDelayMs: Math.Max(0, rep.StandardDelayMs),
                    ShowKeyUpDown: rep.ShowKeyUpDown,
                    CooldownMs: Math.Max(0, rep.CooldownMs),
                    ShowCooldown: showCooldown,
                    IsNameReadOnly: true,
                    SelectedCount: batch.Count,
                    LoopCountMixed: IsMixed(batch, t => Math.Max(0, t.LoopCount)),
                    LoopDelayMixed: IsMixed(batch, t => DelayFormatter.ClampMilliseconds(t.BaseDelayMs)),
                    CooldownMixed: IsMixed(batch, t => DelayFormatter.ClampMilliseconds(t.CooldownMs)),
                    StandardDelayMixed: IsMixed(batch, t => DelayFormatter.ClampMilliseconds(t.StandardDelayMs)),
                    UseStandardDelayMixed: IsMixed(batch, t => t.UseStandardDelay),
                    ShowKeyUpDownMixed: IsMixed(batch, t => t.ShowKeyUpDown),
                    UseTextInputModeMixed: IsMixed(batch, t => t.UseTextInputMode)));

                _window.SetNodeContent(null, false, _isNodeCollapsed);
                return;
            }

            _window.SetTimelineState(new TimelineInspectorState(
                TimelineName: timeline.Name,
                IsNameEditing: ReferenceEquals(_editingTimelineName, timeline),
                IsCollapsed: _isTimelineCollapsed,
                IsEditingEnabled: _canEdit(),
                LoopCount: Math.Max(0, timeline.LoopCount),
                LoopDelayMs: Math.Max(0, timeline.BaseDelayMs),
                UseTextInputMode: timeline.UseTextInputMode,
                UseStandardDelay: timeline.UseStandardDelay,
                StandardDelayMs: Math.Max(0, timeline.StandardDelayMs),
                ShowKeyUpDown: timeline.ShowKeyUpDown,
                CooldownMs: Math.Max(0, timeline.CooldownMs),
                ShowCooldown: showCooldown,
                IsNameReadOnly: workspace.IsHookTimeline(timeline)));

            var nodeContent = _nodeInspectorBuilder.Build(timeline);
            _window.SetNodeContent(nodeContent, nodeContent != null, _isNodeCollapsed);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // True when multiple normal timelines are selected — the inspector then batch-edits all of them.
    private bool TryGetBatchTimelines(out IReadOnlyList<MacroTimeline> timelines)
    {
        if (_selection.HasMultipleTimelineSelection)
        {
            var workspace = _getActiveWorkspace();
            var list = _selection.SelectedTimelines.Where(t => !workspace.IsHookTimeline(t)).ToList();
            if (list.Count > 1)
            {
                timelines = list;
                return true;
            }
        }

        timelines = Array.Empty<MacroTimeline>();
        return false;
    }

    private static bool IsMixed<T>(IReadOnlyList<MacroTimeline> timelines, Func<MacroTimeline, T> selector) =>
        BatchValues.Read(timelines, selector).HasMixedValue;

    public void BeginTimelineNameEdit(MacroTimeline timeline)
    {
        if (!_canEdit())
            return;

        _selection.SelectTimeline(timeline);
        _editingTimelineName = timeline;
        Refresh();
    }

    private void WireWindowEvents()
    {
        _window.TimelineHeaderClicked += ToggleTimelineSection;
        _window.NodeHeaderClicked += ToggleNodeSection;
        _window.TimelineNameEditStarted += BeginTimelineNameEdit;
        _window.TimelineNameCommitted += CommitTimelineName;
        _window.TimelineNameEditCancelled += CancelTimelineNameEdit;
        _window.TimelineLoopsCommitted += CommitLoopCount;
        _window.TimelineLoopDelayCommitted += CommitLoopDelay;
        _window.TimelineInputTypeChangeRequested += ToggleInputType;
        _window.TimelineStandardDelayChanged += SetStandardDelayEnabled;
        _window.TimelineStandardDelayCommitted += CommitStandardDelay;
        _window.TimelineShowKeyUpDownChanged += SetShowKeyUpDown;
        _window.TimelineCooldownCommitted += CommitCooldown;
    }

    private void ToggleTimelineSection()
    {
        _isTimelineCollapsed = !_isTimelineCollapsed;
        Refresh();
    }

    private void ToggleNodeSection()
    {
        _isNodeCollapsed = !_isNodeCollapsed;
        Refresh();
    }

    private void BeginTimelineNameEdit()
    {
        _editingTimelineName = _getCurrentTimeline();
    }

    private void CommitTimelineName(string name)
    {
        var timeline = _editingTimelineName ?? _getCurrentTimeline();

        if (_commitService.CommitTimelineName(timeline, name))
            _editingTimelineName = null;
    }

    private void CancelTimelineNameEdit()
    {
        _editingTimelineName = null;
        Refresh();
    }

    private void CommitLoopCount(int value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t => t.LoopCount = value);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.LoopCount = value);
        Refresh();
    }

    private void CommitLoopDelay(int value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t => t.BaseDelayMs = value);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.BaseDelayMs = value);
    }

    private void CommitCooldown(int value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t => t.CooldownMs = value);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.CooldownMs = value);
    }

    private void ToggleInputType()
    {
        if (TryGetBatchTimelines(out var batch))
        {
            var rep = batch.Contains(_getCurrentTimeline()) ? _getCurrentTimeline() : batch[0];
            var newValue = !rep.UseTextInputMode;
            _commitService.CommitTimelinesChange(batch, t => t.UseTextInputMode = newValue);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.UseTextInputMode = !timeline.UseTextInputMode);
        Refresh();
    }

    private void SetStandardDelayEnabled(bool value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t =>
            {
                t.UseStandardDelay = value;
                if (!t.UseStandardDelay)
                    t.ShowKeyUpDown = true;
            });
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () =>
        {
            timeline.UseStandardDelay = value;
            if (!timeline.UseStandardDelay)
                timeline.ShowKeyUpDown = true;
        });
        Refresh();
    }

    private void CommitStandardDelay(int value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t => t.StandardDelayMs = value);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.StandardDelayMs = value);
    }

    private void SetShowKeyUpDown(bool value)
    {
        if (TryGetBatchTimelines(out var batch))
        {
            _commitService.CommitTimelinesChange(batch, t => t.ShowKeyUpDown = value);
            return;
        }

        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.ShowKeyUpDown = value);
        Refresh();
    }
}
