using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Playback;

namespace KeyLine;

public partial class MainWindow
{
    private bool _isUpdatingHookCheckboxes;

    // Workspaces whose current run should skip the End hook (hard stop).
    private readonly HashSet<MacroWorkspace> _hardStopWorkspaces = new();
    private bool _appIsClosing;

    // --- Stop types ---
    // Normal stop (shortcut stop / natural completion) runs the End hook.
    // Hard stop (Pause>Stop, Emergency Stop, app close) skips the End hook.
    private void RequestHardStop(MacroWorkspace workspace) => _hardStopWorkspaces.Add(workspace);

    private void RequestHardStopAll()
    {
        foreach (var workspace in _workspaces)
            _hardStopWorkspaces.Add(workspace);
    }

    private bool IsHardStopRequested(MacroWorkspace workspace) =>
        _appIsClosing || _hardStopWorkspaces.Contains(workspace);

    // --- Hook-wrapped playback ---
    // Start hook -> body -> End hook. The busy state is held by the caller (MarkShortcutStarting)
    // across the whole wrap, so the macro cannot be re-triggered until End finishes.
    private async Task RunWrappedPlaybackAsync(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged,
        Action<string>? onPlaybackFailure,
        bool followForegroundWindow,
        bool singlePassBody = false)
    {
        _hardStopWorkspaces.Remove(workspace);

        if (!IsHardStopRequested(workspace))
            await RunHookAsync(targetHwnd, workspace, isStart: true, followForegroundWindow, onPlaybackFailure);

        if (singlePassBody)
        {
            // Sequence/Random play a single selected timeline once (loop count forced to 1).
            await _playback.RunSingleTimelineAsync(
                targetHwnd,
                workspace,
                GetWorkspacesForProfile(workspace.ProfileId),
                runnableTimelines[0],
                followForegroundWindow,
                onPlaybackFailure);
        }
        else
        {
            await RunPlaybackForLoopMode(
                targetHwnd,
                workspace,
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged,
                onPlaybackFailure,
                followForegroundWindow);
        }

        if (!IsHardStopRequested(workspace))
            await RunHookAsync(targetHwnd, workspace, isStart: false, followForegroundWindow, onPlaybackFailure);
    }

    private Task RunHookAsync(
        nint targetHwnd,
        MacroWorkspace workspace,
        bool isStart,
        bool followForegroundWindow,
        Action<string>? onPlaybackFailure)
    {
        var enabled = isStart ? workspace.StartHookEnabled : workspace.EndHookEnabled;
        var hook = isStart ? workspace.StartHookTimeline : workspace.EndHookTimeline;

        if (!enabled || !hook.HasNodes)
            return Task.CompletedTask;

        return _playback.RunSingleTimelineAsync(
            targetHwnd,
            workspace,
            GetWorkspacesForProfile(workspace.ProfileId),
            hook,
            followForegroundWindow,
            onPlaybackFailure);
    }

    private void InitializeHooksUi()
    {
        InitializeResetUi();
        HooksPill.MouseLeftButtonDown += HooksPill_MouseLeftButtonDown;
        StartHookCheckBox.Checked += (_, _) => OnHookToggled(isStart: true, enabled: true);
        StartHookCheckBox.Unchecked += (_, _) => OnHookToggled(isStart: true, enabled: false);
        EndHookCheckBox.Checked += (_, _) => OnHookToggled(isStart: false, enabled: true);
        EndHookCheckBox.Unchecked += (_, _) => OnHookToggled(isStart: false, enabled: false);
    }

    private void HooksPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HooksPopup.IsOpen = !HooksPopup.IsOpen;
        e.Handled = true;
    }

    private void ApplyHookOptionsFromWorkspace(MacroWorkspace workspace)
    {
        _isUpdatingHookCheckboxes = true;
        try
        {
            StartHookCheckBox.IsChecked = workspace.StartHookEnabled;
            EndHookCheckBox.IsChecked = workspace.EndHookEnabled;
        }
        finally
        {
            _isUpdatingHookCheckboxes = false;
        }

        UpdateHooksPillText();
    }

    private void OnHookToggled(bool isStart, bool enabled)
    {
        if (_isUpdatingHookCheckboxes)
            return;

        // Hooks are an authoring option; only editable when the timeline is editable (not running).
        if (!_isTimelineEditingEnabled)
        {
            ApplyHookOptionsFromWorkspace(_activeWorkspace);
            return;
        }

        var hook = isStart ? _activeWorkspace.StartHookTimeline : _activeWorkspace.EndHookTimeline;

        if (isStart)
            _activeWorkspace.StartHookEnabled = enabled;
        else
            _activeWorkspace.EndHookEnabled = enabled;

        if (enabled)
        {
            // Enabling a hook makes it visible and active (selected) so it can be authored.
            _selection.SelectTimeline(hook);
        }
        else if (ReferenceEquals(_selection.SelectedTimeline, hook))
        {
            // The hidden hook was selected; fall back to the active normal timeline.
            _selection.SelectTimeline(_document.ActiveTimeline);
        }

        RefreshTimeline();
        UpdateHooksPillText();
        ScheduleSaveState();
    }

    private void UpdateHooksPillText()
    {
        var start = _activeWorkspace.StartHookEnabled;
        var end = _activeWorkspace.EndHookEnabled;

        HooksPill.Text = (start, end) switch
        {
            (true, true) => "Hooks: S+E",
            (true, false) => "Hooks: S",
            (false, true) => "Hooks: E",
            _ => "Hooks"
        };
    }
}
