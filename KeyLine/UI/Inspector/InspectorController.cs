using System.Windows;
using KeyLine.Domain;
using KeyLine.State;

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
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.LoopCount = value);
        Refresh();
    }

    private void CommitLoopDelay(int value)
    {
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.BaseDelayMs = value);
    }

    private void CommitCooldown(int value)
    {
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.CooldownMs = value);
    }

    private void ToggleInputType()
    {
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.UseTextInputMode = !timeline.UseTextInputMode);
        Refresh();
    }

    private void SetStandardDelayEnabled(bool value)
    {
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
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineValueChange(timeline, () => timeline.StandardDelayMs = value);
    }

    private void SetShowKeyUpDown(bool value)
    {
        var timeline = _getCurrentTimeline();
        _commitService.CommitTimelineChange(timeline, () => timeline.ShowKeyUpDown = value);
        Refresh();
    }
}
