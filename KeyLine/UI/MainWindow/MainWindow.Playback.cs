using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;

namespace KeyLine;

public partial class MainWindow
{
    private readonly DispatcherTimer _playbackStatusTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

    private readonly object _loopCountdownLock = new();
    private bool _isPlaybackStatusTimerInitialized;
    private bool _restoreInputsOnStop;
    private bool _isUpdatingPlaybackCounters;
    private readonly Dictionary<MacroWorkspace, WorkspacePlaybackStatusState> _workspacePlaybackStates = new();
    private MacroWorkspace? _timelinePlaybackStatusWorkspace;
    private readonly Dictionary<MacroTimeline, TimelinePlaybackStatus> _timelinePlaybackStatuses = new();
    private int _timelinePlaybackStatusVersion;

    private sealed class WorkspacePlaybackStatusState
    {
        public DateTime TimerDeadlineUtc { get; set; }
        public TimeSpan TimerRemaining { get; set; }
        public int OriginalTimerMs { get; init; }
        public int[] RunnerCompletedLoops { get; init; } = Array.Empty<int>();
        public int[] RunnerTargetLoops { get; init; } = Array.Empty<int>();
        public int RemainingLoopCount { get; set; }
        public bool UsesFocusedWindowTarget { get; init; }
        public string TargetTitle { get; init; } = "";
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsWorkspaceRunning(_activeWorkspace))
        {
            ResumeWorkspacePlayback(_activeWorkspace);
            return;
        }

        if (!ValidateWorkspaceFeaturesForPlayback(_activeWorkspace))
            return;

        var target = GetPlaybackTarget(_activeWorkspace, updateSelection: true);
        if (target == null)
            return;

        var runnableTimelines = _document.Timelines
            .Where(timeline => timeline.Nodes.Count > 0)
            .ToList();

        if (runnableTimelines.Count == 0)
            return;

        var timerMs = GetTimerMs();

        var workspace = _activeWorkspace;
        InitializeWorkspacePlaybackState(workspace, timerMs, runnableTimelines, target);
        _restoreInputsOnStop = false;
        _playback.PrepareManualStart();
        _playback.MarkShortcutStarting(workspace);
        BeginTimelinePlaybackStatuses(workspace, runnableTimelines);

        SetPlaybackUiRunning();
        SetTimelineEditingEnabled(false);
        RefreshMacroTabs();

        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        PlayMacroSound();

        StartPlaybackTimer(workspace, timerMs);
        UpdatePlaybackStatusText();

        await RunPlaybackForLoopMode(
            target.Handle,
            workspace,
            runnableTimelines,
            CreateRunnerLoopCompletedCallback(workspace),
            CreateTimelineStatusCallback(workspace, runnableTimelines),
            CreatePlaybackFailureCallback(workspace));

        _playback.UnmarkShortcutStarting(workspace);
        SetWorkspaceStoppedStatus(workspace, _restoreInputsOnStop);
        PlayMacroSound();
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!IsWorkspaceRunning(_activeWorkspace))
            return;

        if (IsWorkspacePaused(_activeWorkspace))
        {
            ResumeWorkspacePlayback(_activeWorkspace);
            return;
        }

        PauseWorkspaceRunners(_activeWorkspace);
        if (ReferenceEquals(_activeWorkspace, _workspaces[_activeWorkspaceIndex]))
            PausePlaybackTimer(_activeWorkspace);

        SetPauseResumeButtonMode(true);
        PlaybackSplitButton.Visibility = Visibility.Visible;
        StartStopButton.Visibility = Visibility.Collapsed;
        UpdatePlaybackStatusText(paused: true);
        RefreshMacroTabs();
    }

    private void StopPlaybackButton_Click(object sender, RoutedEventArgs e)
    {
        _restoreInputsOnStop = true;
        StopWorkspaceRunners(_activeWorkspace);
        SetStoppedStatus(true);
    }

    private bool AnyPlaybackRunning() => _playback.AnyRunnerRunning;

    private bool AnyPlaybackPaused() => _playback.AnyRunnerPaused;

    private bool TryGetTimelineRunner(MacroTimeline timeline, out MacroRunner runner)
    {
        return _playback.TryGetRunner(timeline, out runner);
    }

    private void RemoveTimelineRunner(MacroTimeline timeline)
    {
        _playback.RemoveRunner(timeline);
    }

    private void StopAllRunners()
    {
        _playback.StopAll();
    }

    private bool IsWorkspaceRunning(MacroWorkspace workspace)
    {
        return _playback.IsWorkspaceRunning(workspace);
    }

    private void StopWorkspaceRunners(MacroWorkspace workspace)
    {
        _playback.StopWorkspace(workspace);
    }

    private void PauseWorkspaceRunners(MacroWorkspace workspace)
    {
        _playback.PauseWorkspace(workspace);
    }

    private void ResumeWorkspaceRunners(MacroWorkspace workspace)
    {
        _playback.ResumeWorkspace(workspace);
    }

    private bool IsWorkspacePaused(MacroWorkspace workspace)
    {
        return _playback.IsWorkspacePaused(workspace);
    }

    private async void StartWorkspacePlaybackFromShortcut(int workspaceIndex)
    {
        if (workspaceIndex < 0 || workspaceIndex >= _workspaces.Count)
            return;

        var workspace = _workspaces[workspaceIndex];
        if (!ValidateWorkspaceFeaturesForPlayback(workspace))
            return;

        if (!_playback.MarkShortcutStarting(workspace))
            return;
        RefreshMacroTabs();

        var target = GetPlaybackTarget(workspace, updateSelection: workspaceIndex == _activeWorkspaceIndex);
        if (target == null)
        {
            _playback.UnmarkShortcutStarting(workspace);
            RefreshMacroTabs();
            if (workspaceIndex == _activeWorkspaceIndex && string.IsNullOrWhiteSpace(workspace.ErrorMessage))
                StatusText.Text = "Shortcut target not found";
            return;
        }

        var runnableTimelines = workspace.Document.Timelines
            .Where(timeline => timeline.Nodes.Count > 0)
            .ToList();

        if (runnableTimelines.Count == 0)
        {
            _playback.UnmarkShortcutStarting(workspace);
            RefreshMacroTabs();
            if (workspaceIndex == _activeWorkspaceIndex)
                StatusText.Text = "No steps to run";
            return;
        }

        var timerMs = Math.Max(0, workspace.TimerMs);
        InitializeWorkspacePlaybackState(workspace, timerMs, runnableTimelines, target);

        if (workspaceIndex == _activeWorkspaceIndex)
        {
            BeginTimelinePlaybackStatuses(workspace, runnableTimelines);
            SetPlaybackUiRunning();
            SetTimelineEditingEnabled(false);
            StartPlaybackTimer(workspace, timerMs);
            UpdatePlaybackStatusText();
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        }
        else
        {
            StartPlaybackTimer(workspace, timerMs, updateUi: false);
        }

        var completionTask = RunPlaybackForLoopMode(
            target.Handle,
            workspace,
            runnableTimelines,
            CreateRunnerLoopCompletedCallback(workspace),
            onTimelineStatusChanged: CreateTimelineStatusCallback(workspace, runnableTimelines),
            onPlaybackFailure: CreatePlaybackFailureCallback(workspace));

        PlayMacroSound();

        try
        {
            if (timerMs > 0 && await Task.WhenAny(completionTask, Task.Delay(timerMs)) != completionTask)
                StopWorkspaceRunners(workspace);

            await completionTask;
        }
        finally
        {
            _playback.UnmarkShortcutStarting(workspace);
            RefreshMacroTabs();

            if (!IsWorkspaceRunning(workspace))
                SetWorkspaceStoppedStatus(workspace);

            PlayMacroSound();
        }
    }

    private void StopAllPlaybackFromGlobalShortcut()
    {
        _restoreInputsOnStop = true;
        StopAllRunners();
        SetStoppedStatus(restoreInputs: true, clearAllStates: true);
        PlayMacroSound();
    }

    private Task RunPlaybackForLoopMode(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null,
        Action<string>? onPlaybackFailure = null)
    {
        return workspace.LoopMode switch
        {
            MacroLoopMode.Chain => _playback.RunChainPlayback(
                targetHwnd,
                workspace,
                GetWorkspacesForProfile(workspace.ProfileId),
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged,
                onPlaybackFailure),
            MacroLoopMode.Cycle => _playback.RunCyclePlayback(
                targetHwnd,
                workspace,
                GetWorkspacesForProfile(workspace.ProfileId),
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged,
                onPlaybackFailure),
            MacroLoopMode.Sync when runnableTimelines.Count > 1 => _playback.RunSyncedPlayback(
                targetHwnd,
                workspace,
                GetWorkspacesForProfile(workspace.ProfileId),
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged,
                onPlaybackFailure),
            _ => _playback.RunAsyncPlayback(
                targetHwnd,
                workspace,
                GetWorkspacesForProfile(workspace.ProfileId),
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged,
                onPlaybackFailure)
        };
    }

    private void PauseResumeAllPlaybackFromGlobalShortcut()
    {
        if (!AnyPlaybackRunning())
            return;

        if (AnyPlaybackPaused())
        {
            _playback.ResumeAll();
            ResumePlaybackTimer(_activeWorkspace);
            UpdatePlaybackStatusText();
            return;
        }

        _playback.PauseAll();
        PausePlaybackTimer(_activeWorkspace);
        UpdatePlaybackStatusText(paused: true);
    }

    private void SetPlaybackUiRunning()
    {
        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;
        StartStopButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.Visibility = Visibility.Visible;
        PlaybackSplitButton.IsHitTestVisible = true;
        SetPauseResumeButtonMode(false);
    }

    private void ResumeWorkspacePlayback(MacroWorkspace workspace)
    {
        ResumeWorkspaceRunners(workspace);
        if (ReferenceEquals(workspace, _activeWorkspace))
            ResumePlaybackTimer(workspace);

        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;
        StartStopButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.Visibility = Visibility.Visible;
        PlaybackSplitButton.IsHitTestVisible = true;
        SetPauseResumeButtonMode(false);
        UpdatePlaybackStatusText();
        RefreshMacroTabs();
    }

    private void RefreshActiveWorkspacePlaybackUi()
    {
        if (IsWorkspaceRunning(_activeWorkspace))
        {
            SetPlaybackUiRunning();
            SetTimelineEditingEnabled(false);
            RefreshMacroTabs();
            SetPauseResumeButtonMode(IsWorkspacePaused(_activeWorkspace));
            UpdatePlaybackTimerStatus();
            if (!IsWorkspaceRunning(_activeWorkspace))
                return;

            UpdatePlaybackStatusText(paused: IsWorkspacePaused(_activeWorkspace));
            return;
        }

        SetStoppedStatus(resetTimer: false);
    }

    private void SetStoppedStatus(bool restoreInputs = false, bool resetTimer = true, bool clearAllStates = false)
    {
        var originalTimerMs = GetWorkspaceOriginalTimerMs(_activeWorkspace);
        if (clearAllStates)
            _workspacePlaybackStates.Clear();
        else
            _workspacePlaybackStates.Remove(_activeWorkspace);

        if (!HasRunningTimerState())
            _playbackStatusTimer.Stop();
        ClearTimelinePlaybackStatuses();

        if (resetTimer)
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, originalTimerMs);

        StartStopButton.Content = "\u25B6  Start";
        StartStopButton.Visibility = Visibility.Visible;
        SetPauseResumeButtonMode(false);
        PlaybackSplitButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.IsHitTestVisible = false;
        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;

        SetTimelineEditingEnabled(true);
        if (string.IsNullOrWhiteSpace(_activeWorkspace.ErrorMessage))
        {
            StatusText.Text = "Stopped";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
        }
        else
        {
            StatusText.Text = _activeWorkspace.ErrorMessage;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }
        SetCountdownRunningStyle(false);
        SyncOptionsFromActiveTimeline();
        RefreshMacroTabs();
    }

    private void SetWorkspaceStoppedStatus(MacroWorkspace workspace, bool restoreInputs = false)
    {
        var isActiveWorkspace = ReferenceEquals(workspace, _activeWorkspace);

        if (isActiveWorkspace)
        {
            SetStoppedStatus(restoreInputs);
        }
        else
        {
            _workspacePlaybackStates.Remove(workspace);
            if (!HasRunningTimerState())
                _playbackStatusTimer.Stop();

            RefreshMacroTabs();
        }
    }

    private void InitializeWorkspacePlaybackState(
        MacroWorkspace workspace,
        int timerMs,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        TargetWindowInfo? target = null)
    {
        var state = new WorkspacePlaybackStatusState
        {
            OriginalTimerMs = Math.Max(0, timerMs),
            RunnerCompletedLoops = new int[runnableTimelines.Count],
            RunnerTargetLoops = runnableTimelines
                .Select(timeline => Math.Max(0, timeline.LoopCount))
                .ToArray(),
            UsesFocusedWindowTarget = target?.IsFocusedWindowFallback == true,
            TargetTitle = target?.Title ?? ""
        };

        state.RemainingLoopCount = GetDisplayedRemainingLoopCount(state);
        _workspacePlaybackStates[workspace] = state;
    }

    private int GetWorkspaceOriginalTimerMs(MacroWorkspace workspace)
    {
        return _workspacePlaybackStates.TryGetValue(workspace, out var state)
            ? state.OriginalTimerMs
            : Math.Max(0, workspace.TimerMs);
    }

    private void StartPlaybackTimer(MacroWorkspace workspace, int milliseconds, bool updateUi = true)
    {
        EnsurePlaybackStatusTimerInitialized();
        var state = GetOrCreateWorkspacePlaybackState(workspace, milliseconds);

        if (milliseconds <= 0)
        {
            state.TimerRemaining = TimeSpan.Zero;
            state.TimerDeadlineUtc = DateTime.MinValue;
            if (updateUi && ReferenceEquals(workspace, _activeWorkspace))
            {
                SetCountdownRunningStyle(true);
                UpdatePlaybackStatusText();
            }
            return;
        }

        state.TimerRemaining = TimeSpan.FromMilliseconds(milliseconds);
        state.TimerDeadlineUtc = DateTime.UtcNow + state.TimerRemaining;
        _playbackStatusTimer.Start();
        if (updateUi && ReferenceEquals(workspace, _activeWorkspace))
            UpdatePlaybackTimerStatus();
    }

    private void PausePlaybackTimer(MacroWorkspace workspace)
    {
        if (!_workspacePlaybackStates.TryGetValue(workspace, out var state))
            return;

        if (state.TimerDeadlineUtc == DateTime.MinValue)
            return;

        state.TimerRemaining = state.TimerDeadlineUtc - DateTime.UtcNow;
        if (state.TimerRemaining < TimeSpan.Zero)
            state.TimerRemaining = TimeSpan.Zero;

        state.TimerDeadlineUtc = DateTime.MinValue;
        if (!HasRunningTimerState())
            _playbackStatusTimer.Stop();
    }

    private void ResumePlaybackTimer(MacroWorkspace workspace)
    {
        if (!_workspacePlaybackStates.TryGetValue(workspace, out var state))
            return;

        if (state.TimerRemaining <= TimeSpan.Zero)
            return;

        state.TimerDeadlineUtc = DateTime.UtcNow + state.TimerRemaining;
        _playbackStatusTimer.Start();
        UpdatePlaybackTimerStatus();
    }

    private void EnsurePlaybackStatusTimerInitialized()
    {
        if (_isPlaybackStatusTimerInitialized)
            return;

        _playbackStatusTimer.Tick += (_, _) => UpdatePlaybackTimerStatus();
        _isPlaybackStatusTimerInitialized = true;
    }

    private void UpdatePlaybackTimerStatus()
    {
        var now = DateTime.UtcNow;

        foreach (var (workspace, state) in _workspacePlaybackStates.ToList())
        {
            if (state.TimerDeadlineUtc == DateTime.MinValue)
                continue;

            var remaining = state.TimerDeadlineUtc - now;
            if (remaining <= TimeSpan.Zero)
            {
                state.TimerRemaining = TimeSpan.Zero;
                if (ReferenceEquals(workspace, _activeWorkspace))
                    StatusText.Text = "Timer elapsed; stopping";

                StopWorkspaceRunners(workspace);
                continue;
            }

            state.TimerRemaining = remaining;
        }

        if (!HasRunningTimerState())
            _playbackStatusTimer.Stop();

        if (IsWorkspaceRunning(_activeWorkspace))
        {
            UpdatePlaybackStatusText(paused: IsWorkspacePaused(_activeWorkspace));
            SetCountdownRunningStyle(true);
        }
    }

    private Action<int> CreateRunnerLoopCompletedCallback(MacroWorkspace workspace)
    {
        return runnerIndex => OnRunnerLoopCompleted(workspace, runnerIndex);
    }

    private void OnRunnerLoopCompleted(MacroWorkspace workspace, int runnerIndex)
    {
        if (!_workspacePlaybackStates.TryGetValue(workspace, out var state))
            return;

        if (runnerIndex < 0 || runnerIndex >= state.RunnerCompletedLoops.Length)
            return;

        var shouldStop = false;

        lock (_loopCountdownLock)
        {
            state.RunnerCompletedLoops[runnerIndex]++;
            var remaining = GetDisplayedRemainingLoopCount(state);

            if (remaining != state.RemainingLoopCount)
            {
                state.RemainingLoopCount = remaining;
                shouldStop = state.RunnerTargetLoops.Length > 0 &&
                             state.RunnerTargetLoops.All(target => target > 0) &&
                             remaining <= 0;
            }
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ReferenceEquals(workspace, _activeWorkspace))
                UpdatePlaybackStatusText();
            if (shouldStop)
                StopWorkspaceRunners(workspace);
        }));
    }

    private int GetDisplayedRemainingLoopCount(WorkspacePlaybackStatusState? state = null)
    {
        state ??= _workspacePlaybackStates.TryGetValue(_activeWorkspace, out var activeState)
            ? activeState
            : null;

        if (state == null || state.RunnerTargetLoops.Length == 0 || state.RunnerCompletedLoops.Length == 0)
            return 0;

        if (state.RunnerTargetLoops.Any(target => target <= 0))
            return int.MaxValue;

        var remaining = 0;

        for (var i = 0; i < state.RunnerTargetLoops.Length && i < state.RunnerCompletedLoops.Length; i++)
            remaining = Math.Max(remaining, Math.Max(0, state.RunnerTargetLoops[i] - state.RunnerCompletedLoops[i]));

        return remaining;
    }

    private string GetPlaybackLoopCounterText()
    {
        var remaining = GetDisplayedRemainingLoopCount();
        return remaining == int.MaxValue ? "\u221E" : remaining.ToString();
    }

    private void BeginTimelinePlaybackStatuses(
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> runnableTimelines)
    {
        if (!ReferenceEquals(workspace, _activeWorkspace))
            return;

        _timelinePlaybackStatusWorkspace = workspace;
        _timelinePlaybackStatusVersion++;
        _timelinePlaybackStatuses.Clear();

        var runnableSet = new HashSet<MacroTimeline>(runnableTimelines);
        var firstRunnableTimeline = runnableTimelines.FirstOrDefault();

        foreach (var timeline in workspace.Document.Timelines)
        {
            if (!runnableSet.Contains(timeline))
            {
                _timelinePlaybackStatuses[timeline] = TimelinePlaybackStatus.Stopped;
                continue;
            }

            _timelinePlaybackStatuses[timeline] =
                workspace.LoopMode is MacroLoopMode.Cycle or MacroLoopMode.Chain &&
                !ReferenceEquals(timeline, firstRunnableTimeline)
                    ? TimelinePlaybackStatus.Waiting
                    : TimelinePlaybackStatus.Running;
        }

        RefreshTimelineHeaderStatuses();
    }

    private Action<int, TimelinePlaybackStatus> CreateTimelineStatusCallback(
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> runnableTimelines)
    {
        var callbackVersion = _timelinePlaybackStatusVersion;

        return (timelineIndex, status) =>
        {
            if (timelineIndex < 0 || timelineIndex >= runnableTimelines.Count)
                return;

            var timeline = runnableTimelines[timelineIndex];

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (callbackVersion != _timelinePlaybackStatusVersion)
                    return;

                if (!ReferenceEquals(workspace, _activeWorkspace))
                    return;

                _timelinePlaybackStatusWorkspace = workspace;
                _timelinePlaybackStatuses[timeline] = status;
                UpdateTimelineHeaderPlaybackStatus(timeline);
            }), DispatcherPriority.Background);
        };
    }

    private Action<string> CreatePlaybackFailureCallback(MacroWorkspace workspace)
    {
        return message =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetMacroError(workspace, message);
                StopWorkspaceRunners(workspace);
                RefreshTimelineHeaderStatuses();
            }), DispatcherPriority.Background);
        };
    }

    private void ClearTimelinePlaybackStatuses()
    {
        _timelinePlaybackStatusWorkspace = null;
        _timelinePlaybackStatusVersion++;
        _timelinePlaybackStatuses.Clear();
        RefreshTimelineHeaderStatuses();
    }

    private TimelinePlaybackStatus GetTimelinePlaybackStatusForHeader(MacroTimeline timeline)
    {
        if (HasChainTimelineWarning(timeline))
            return TimelinePlaybackStatus.Warning;

        if (!IsWorkspaceRunning(_activeWorkspace))
            return TimelinePlaybackStatus.Idle;

        if (!ReferenceEquals(_timelinePlaybackStatusWorkspace, _activeWorkspace))
            return TimelinePlaybackStatus.Idle;

        if (_timelinePlaybackStatuses.TryGetValue(timeline, out var status))
            return status;

        if (!timeline.HasNodes)
            return TimelinePlaybackStatus.Stopped;

        return TryGetTimelineRunner(timeline, out var runner) && runner.IsRunning
            ? TimelinePlaybackStatus.Running
            : TimelinePlaybackStatus.Waiting;
    }

    private bool HasChainTimelineWarning(MacroTimeline timeline)
    {
        if (_activeWorkspace.LoopMode != MacroLoopMode.Chain)
            return false;

        if (!timeline.HasNodes || Math.Max(0, timeline.LoopCount) > 0)
            return false;

        var timelines = _activeWorkspace.Document.Timelines;
        var index = timelines.IndexOf(timeline);
        return index >= 0 && index < timelines.Count - 1;
    }

    private void UpdatePlaybackStatusText(bool paused = false)
    {
        var remainingText = GetPlaybackRemainingText();
        SetTimerCountdownText(remainingText);

        StatusText.Text = paused
            ? $"Paused; {remainingText} remaining"
            : GetRunningStatusText(remainingText);

        StatusText.Foreground = new SolidColorBrush(paused
            ? Color.FromRgb(253, 230, 138)
            : Color.FromRgb(52, 211, 153));
    }

    private string GetPlaybackRemainingText()
    {
        if (!_workspacePlaybackStates.TryGetValue(_activeWorkspace, out var state))
            return "\u221E";

        if (state.OriginalTimerMs <= 0)
            return "\u221E";

        var remaining = state.TimerDeadlineUtc == DateTime.MinValue
            ? state.TimerRemaining
            : state.TimerDeadlineUtc - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return FormatRemainingTime(remaining);
    }

    private string GetRunningStatusText(string remainingText)
    {
        if (!_workspacePlaybackStates.TryGetValue(_activeWorkspace, out var state) ||
            !state.UsesFocusedWindowTarget)
        {
            return $"Running... {remainingText} remaining";
        }

        var target = string.IsNullOrWhiteSpace(state.TargetTitle)
            ? "focused window"
            : state.TargetTitle;

        return $"Running on focused window ({target}); {remainingText} remaining";
    }

    private WorkspacePlaybackStatusState GetOrCreateWorkspacePlaybackState(MacroWorkspace workspace, int timerMs)
    {
        if (_workspacePlaybackStates.TryGetValue(workspace, out var state))
            return state;

        state = new WorkspacePlaybackStatusState
        {
            OriginalTimerMs = Math.Max(0, timerMs)
        };
        _workspacePlaybackStates[workspace] = state;
        return state;
    }

    private bool HasRunningTimerState()
    {
        return _workspacePlaybackStates.Values.Any(state => state.TimerDeadlineUtc != DateTime.MinValue);
    }

    private void SetTimelineEditingEnabled(bool isEnabled)
    {
        _isTimelineEditingEnabled = isEnabled;

        if (!isEnabled)
            TimelineAddMenu.Close();

        AddTimelineButton.IsEnabled = isEnabled;
        ClearButton.IsEnabled = isEnabled;
        AddMacroTabButton.IsEnabled = true;
        
        SetMacroOptionsEditingEnabled(isEnabled);
        
        RefreshInspector();
    }

    private static string FormatRemainingTime(TimeSpan remaining)
    {
        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours:0}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:0}:{remaining.Seconds:00}";
    }

    private void SetPauseResumeButtonMode(bool isResume)
    {
        PauseResumeButton.Content = isResume ? "Resume" : "Pause";

        if (isResume)
        {
            PauseResumeButton.Background = new SolidColorBrush(Color.FromRgb(6, 58, 42));
            PauseResumeButton.BorderBrush = new SolidColorBrush(Color.FromRgb(5, 150, 105));
            PauseResumeButton.Foreground = new SolidColorBrush(Color.FromRgb(167, 243, 208));
            return;
        }

        PauseResumeButton.Background = new SolidColorBrush(Color.FromRgb(59, 42, 5));
        PauseResumeButton.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 83, 9));
        PauseResumeButton.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
    }

    private void SetPlaybackCounterText(System.Windows.Controls.TextBox textBox, string text)
    {
        _isUpdatingPlaybackCounters = true;
        try
        {
            textBox.Text = text;
        }
        finally
        {
            _isUpdatingPlaybackCounters = false;
        }
    }

    private void SetTimerCountdownText(string text)
    {
        SetPlaybackCounterText(TimerMinutesTextBox, text);
        TimerUnitTextBlock.Text = "";
    }

    private void SetCountdownRunningStyle(bool isRunning)
    {
        if (isRunning)
        {
            var brush = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            TimerMinutesTextBox.Foreground = brush;
            TimerUnitTextBlock.Foreground = brush;
            return;
        }

        TimerMinutesTextBox.ClearValue(ForegroundProperty);
        TimerUnitTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(142, 160, 182));
    }

    private void SetRemainingLoopStatus(string remainingText)
    {
        if (remainingText == "\u221E")
            StatusText.Text = "Running; loops \u221E";
        else
            StatusText.Text = $"Running; loops left {remainingText}";
    }
}

