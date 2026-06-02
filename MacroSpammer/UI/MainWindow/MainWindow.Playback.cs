using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Playback;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly DispatcherTimer _playbackStatusTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(250)
    };

    private readonly object _loopCountdownLock = new();
    private DateTime _playbackTimerDeadlineUtc;
    private TimeSpan _playbackTimerRemaining;
    private bool _isPlaybackStatusTimerInitialized;
    private int _originalTimerMs;
    private int[] _runnerCompletedLoops = Array.Empty<int>();
    private int[] _runnerTargetLoops = Array.Empty<int>();
    private int _remainingLoopCount;
    private bool _restoreInputsOnStop;
    private bool _isUpdatingPlaybackCounters;
    private MacroWorkspace? _timelinePlaybackStatusWorkspace;
    private readonly Dictionary<MacroTimeline, TimelinePlaybackStatus> _timelinePlaybackStatuses = new();
    private int _timelinePlaybackStatusVersion;

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsWorkspaceRunning(_activeWorkspace))
        {
            ResumeWorkspacePlayback(_activeWorkspace);
            return;
        }

        var target = GetPlaybackTarget(_activeWorkspace, updateSelection: true);
        if (target == null)
            return;

        var runnableTimelines = _document.Timelines
            .Where(timeline => timeline.Nodes.Count > 0)
            .ToList();

        if (runnableTimelines.Count == 0)
            return;

        var timerMs = GetTimerMs();

        _originalTimerMs = timerMs;
        _runnerCompletedLoops = new int[runnableTimelines.Count];
        _runnerTargetLoops = runnableTimelines
            .Select(timeline => Math.Max(0, timeline.LoopCount))
            .ToArray();
        _remainingLoopCount = GetDisplayedRemainingLoopCount();
        _restoreInputsOnStop = false;
        _playback.PrepareManualStart();
        _playback.MarkShortcutStarting(_activeWorkspace);
        BeginTimelinePlaybackStatuses(_activeWorkspace, runnableTimelines);

        SetPlaybackUiRunning();
        SetTimelineEditingEnabled(false);
        RefreshMacroTabs();

        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        PlayMacroSound();

        StartPlaybackTimer(timerMs);
        UpdatePlaybackStatusText();

        await RunPlaybackForLoopType(
            target.Handle,
            _activeWorkspace,
            runnableTimelines,
            OnRunnerLoopCompleted,
            CreateTimelineStatusCallback(_activeWorkspace, runnableTimelines));

        _playback.UnmarkShortcutStarting(_activeWorkspace);
        SetStoppedStatus(_restoreInputsOnStop);
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
            PausePlaybackTimer();

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

        if (workspaceIndex == _activeWorkspaceIndex)
        {
            _originalTimerMs = timerMs;
            BeginTimelinePlaybackStatuses(workspace, runnableTimelines);
            SetPlaybackUiRunning();
            SetTimelineEditingEnabled(false);
            StartPlaybackTimer(timerMs);
            UpdatePlaybackStatusText();
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
        }

        var completionTask = RunPlaybackForLoopType(
            target.Handle,
            workspace,
            runnableTimelines,
            onTimelineStatusChanged: CreateTimelineStatusCallback(workspace, runnableTimelines));

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

            if (workspaceIndex == _activeWorkspaceIndex && !IsWorkspaceRunning(workspace))
                SetStoppedStatus();

            PlayMacroSound();
        }
    }

    private void StopAllPlaybackFromGlobalShortcut()
    {
        _restoreInputsOnStop = true;
        StopAllRunners();
        SetStoppedStatus(true);
        PlayMacroSound();
    }

    private Task RunPlaybackForLoopType(
        nint targetHwnd,
        MacroWorkspace workspace,
        IReadOnlyList<MacroTimeline> runnableTimelines,
        Action<int>? onRunnerLoopCompleted = null,
        Action<int, TimelinePlaybackStatus>? onTimelineStatusChanged = null)
    {
        return workspace.LoopType switch
        {
            MacroLoopType.Sequence => _playback.RunSequencePlayback(
                targetHwnd,
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged),
            MacroLoopType.Sync when runnableTimelines.Count > 1 => _playback.RunSyncedPlayback(
                targetHwnd,
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged),
            _ => _playback.RunAsyncPlayback(
                targetHwnd,
                runnableTimelines,
                onRunnerLoopCompleted,
                onTimelineStatusChanged)
        };
    }

    private void PauseResumeAllPlaybackFromGlobalShortcut()
    {
        if (!AnyPlaybackRunning())
            return;

        if (AnyPlaybackPaused())
        {
            _playback.ResumeAll();
            ResumePlaybackTimer();
            UpdatePlaybackStatusText();
            return;
        }

        _playback.PauseAll();
        PausePlaybackTimer();
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
            ResumePlaybackTimer();

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
            UpdatePlaybackStatusText(paused: IsWorkspacePaused(_activeWorkspace));
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
            return;
        }

        SetStoppedStatus(resetTimer: false);
    }

    private void SetStoppedStatus(bool restoreInputs = false, bool resetTimer = true)
    {
        _playbackStatusTimer.Stop();
        _playbackTimerRemaining = TimeSpan.Zero;
        _playbackTimerDeadlineUtc = DateTime.MinValue;
        _runnerCompletedLoops = Array.Empty<int>();
        _runnerTargetLoops = Array.Empty<int>();
        _remainingLoopCount = 0;
        ClearTimelinePlaybackStatuses();

        if (resetTimer)
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, _originalTimerMs);

        StartStopButton.Content = "\u25B6  Start";
        StartStopButton.Visibility = Visibility.Visible;
        SetPauseResumeButtonMode(false);
        PlaybackSplitButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.IsHitTestVisible = false;
        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;

        SetTimelineEditingEnabled(true);
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
        SetCountdownRunningStyle(false);
        SyncOptionsFromActiveTimeline();
        RefreshMacroTabs();
    }

    private void StartPlaybackTimer(int milliseconds)
    {
        EnsurePlaybackStatusTimerInitialized();

        if (milliseconds <= 0)
        {
            _playbackTimerRemaining = TimeSpan.Zero;
            _playbackTimerDeadlineUtc = DateTime.MinValue;
            SetCountdownRunningStyle(true);
            UpdatePlaybackStatusText();
            return;
        }

        _playbackTimerRemaining = TimeSpan.FromMilliseconds(milliseconds);
        _playbackTimerDeadlineUtc = DateTime.UtcNow + _playbackTimerRemaining;
        _playbackStatusTimer.Start();
        UpdatePlaybackTimerStatus();
    }

    private void PausePlaybackTimer()
    {
        if (_playbackTimerDeadlineUtc == DateTime.MinValue)
            return;

        _playbackTimerRemaining = _playbackTimerDeadlineUtc - DateTime.UtcNow;
        if (_playbackTimerRemaining < TimeSpan.Zero)
            _playbackTimerRemaining = TimeSpan.Zero;

        _playbackTimerDeadlineUtc = DateTime.MinValue;
        _playbackStatusTimer.Stop();
    }

    private void ResumePlaybackTimer()
    {
        if (_playbackTimerRemaining <= TimeSpan.Zero)
            return;

        _playbackTimerDeadlineUtc = DateTime.UtcNow + _playbackTimerRemaining;
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
        if (_playbackTimerDeadlineUtc == DateTime.MinValue)
            return;

        var remaining = _playbackTimerDeadlineUtc - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            StatusText.Text = "Timer elapsed; stopping";
            StopWorkspaceRunners(_activeWorkspace);
            _playbackStatusTimer.Stop();
            return;
        }

        _playbackTimerRemaining = remaining;
        UpdatePlaybackStatusText();
        SetCountdownRunningStyle(true);
    }

    private void OnRunnerLoopCompleted(int runnerIndex)
    {
        if (runnerIndex < 0 || runnerIndex >= _runnerCompletedLoops.Length)
            return;

        var shouldStop = false;

        lock (_loopCountdownLock)
        {
            _runnerCompletedLoops[runnerIndex]++;
            var remaining = GetDisplayedRemainingLoopCount();

            if (remaining != _remainingLoopCount)
            {
                _remainingLoopCount = remaining;
                shouldStop = _runnerTargetLoops.Length > 0 &&
                             _runnerTargetLoops.All(target => target > 0) &&
                             remaining <= 0;
            }
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdatePlaybackStatusText();
            if (shouldStop)
                StopWorkspaceRunners(_activeWorkspace);
        }));
    }

    private int GetDisplayedRemainingLoopCount()
    {
        if (_runnerTargetLoops.Length == 0 || _runnerCompletedLoops.Length == 0)
            return 0;

        if (_runnerTargetLoops.Any(target => target <= 0))
            return int.MaxValue;

        var remaining = 0;

        for (var i = 0; i < _runnerTargetLoops.Length && i < _runnerCompletedLoops.Length; i++)
            remaining = Math.Max(remaining, Math.Max(0, _runnerTargetLoops[i] - _runnerCompletedLoops[i]));

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
                workspace.LoopType == MacroLoopType.Sequence &&
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

    private void ClearTimelinePlaybackStatuses()
    {
        _timelinePlaybackStatusWorkspace = null;
        _timelinePlaybackStatusVersion++;
        _timelinePlaybackStatuses.Clear();
        RefreshTimelineHeaderStatuses();
    }

    private TimelinePlaybackStatus GetTimelinePlaybackStatusForHeader(MacroTimeline timeline)
    {
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

    private void UpdatePlaybackStatusText(bool paused = false)
    {
        var remainingText = GetPlaybackRemainingText();
        StatusText.Text = paused
            ? $"Paused; {remainingText} remaining"
            : $"Running... {remainingText} remaining";

        StatusText.Foreground = new SolidColorBrush(paused
            ? Color.FromRgb(253, 230, 138)
            : Color.FromRgb(52, 211, 153));
    }

    private string GetPlaybackRemainingText()
    {
        if (_originalTimerMs <= 0)
            return "\u221E";

        var remaining = _playbackTimerDeadlineUtc == DateTime.MinValue
            ? _playbackTimerRemaining
            : _playbackTimerDeadlineUtc - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        return FormatRemainingTime(remaining);
    }

    private void SetTimelineEditingEnabled(bool isEnabled)
    {
        _isTimelineEditingEnabled = isEnabled;

        if (!isEnabled)
            AddPopup.IsOpen = false;

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
