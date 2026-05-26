using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

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
    private bool _isPlaybackPaused;
    private bool _isPlaybackStatusTimerInitialized;
    private string _originalLoopText = "0";
    private string _originalTimerText = "0";
    private int[] _runnerCompletedLoops = Array.Empty<int>();
    private int _remainingLoopCount;
    private bool _restoreInputsOnStop;
    private bool _isUpdatingPlaybackCounters;

    private int GetStandardDelayMs() =>
        int.TryParse(StandardDelayTextBox.Text, out var ms) ? Math.Max(0, ms) : 50;

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
        {
            if (_isPlaybackPaused)
                ResumePausedPlayback();

            return;
        }

        var target = GetTargetHandle();
        if (target == null)
            return;

        var runnableTimelines = _document.Timelines
            .Where(timeline => timeline.Steps.Count > 0)
            .ToList();

        if (runnableTimelines.Count == 0)
            return;

        var loopCount = int.TryParse(LoopCountTextBox.Text, out var loops) ? Math.Max(0, loops) : 0;
        var timerMinutes = GetTimerMinutes();

        _originalLoopText = LoopCountTextBox.Text;
        _originalTimerText = TimerMinutesTextBox.Text;
        _remainingLoopCount = loopCount;
        _runnerCompletedLoops = new int[runnableTimelines.Count];
        _restoreInputsOnStop = false;

        SetPlaybackUiRunning();
        SetTimelineEditingEnabled(false);

        StatusText.Text = $"Running {runnableTimelines.Count} timeline(s)";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

        if (loopCount > 0)
            SetPlaybackCounterText(LoopCountTextBox, loopCount.ToString());

        StartPlaybackTimer(timerMinutes);

        var tasks = new List<Task>();

        for (var i = 0; i < runnableTimelines.Count; i++)
        {
            var timeline = runnableTimelines[i];
            var runnerIndex = i;
            var runner = GetRunner(timeline);

            tasks.Add(Task.Run(() => runner.StartAsync(
                target.Handle,
                timeline.Steps.ToList(),
                loopCount,
                timeline.UseStandardDelay,
                timeline.StandardDelayMs,
                () => OnRunnerLoopCompleted(runnerIndex, loopCount))));
        }

        await Task.WhenAll(tasks);

        SetStoppedStatus(_restoreInputsOnStop);
    }

    private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_runners.Values.Any(runner => runner.IsRunning))
            return;

        if (_isPlaybackPaused)
        {
            ResumePausedPlayback();
            return;
        }

        PauseAllRunners();
        PausePlaybackTimer();
        _isPlaybackPaused = true;
        SetPauseResumeButtonMode(true);
        PlaybackSplitButton.Visibility = Visibility.Visible;
        StartStopButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Paused";
    }

    private void StopPlaybackButton_Click(object sender, RoutedEventArgs e)
    {
        _restoreInputsOnStop = true;
        StopAllRunners();
        SetStoppedStatus(true);
    }

    private MacroRunner GetRunner(MacroTimeline timeline)
    {
        if (_runners.TryGetValue(timeline, out var runner))
            return runner;

        runner = new MacroRunner();
        _runners[timeline] = runner;
        return runner;
    }

    private void StopAllRunners()
    {
        foreach (var runner in _runners.Values)
            runner.Stop();
    }

    private void PauseAllRunners()
    {
        foreach (var runner in _runners.Values)
            runner.Pause();
    }

    private void ResumeAllRunners()
    {
        foreach (var runner in _runners.Values)
            runner.Resume();
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

    private void ResumePausedPlayback()
    {
        ResumeAllRunners();
        ResumePlaybackTimer();
        _isPlaybackPaused = false;
        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;
        StartStopButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.Visibility = Visibility.Visible;
        PlaybackSplitButton.IsHitTestVisible = true;
        SetPauseResumeButtonMode(false);
        StatusText.Text = "Running";
    }

    private void SetStoppedStatus(bool restoreInputs = false)
    {
        _playbackStatusTimer.Stop();
        _playbackTimerRemaining = TimeSpan.Zero;
        _playbackTimerDeadlineUtc = DateTime.MinValue;
        _isPlaybackPaused = false;
        _runnerCompletedLoops = Array.Empty<int>();
        _remainingLoopCount = 0;

        if (restoreInputs)
        {
            SetPlaybackCounterText(LoopCountTextBox, _originalLoopText);
            SetPlaybackCounterText(TimerMinutesTextBox, _originalTimerText);
        }

        StartStopButton.Content = "▶  Start";
        StartStopButton.Visibility = Visibility.Visible;
        SetPauseResumeButtonMode(false);
        PlaybackSplitButton.Visibility = Visibility.Collapsed;
        PlaybackSplitButton.IsHitTestVisible = false;
        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;

        SetTimelineEditingEnabled(true);
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
    }

    private void StartPlaybackTimer(int minutes)
    {
        EnsurePlaybackStatusTimerInitialized();

        if (minutes <= 0)
        {
            _playbackTimerRemaining = TimeSpan.Zero;
            _playbackTimerDeadlineUtc = DateTime.MinValue;
            return;
        }

        _playbackTimerRemaining = TimeSpan.FromMinutes(minutes);
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
            SetPlaybackCounterText(TimerMinutesTextBox, "0");
            StatusText.Text = "Timer elapsed; stopping";
            StopAllRunners();
            _playbackStatusTimer.Stop();
            return;
        }

        SetPlaybackCounterText(TimerMinutesTextBox, Math.Ceiling(remaining.TotalMinutes).ToString("0"));
        StatusText.Text = $"Running - {FormatRemainingTime(remaining)} left";
    }

    private void OnRunnerLoopCompleted(int runnerIndex, int loopCount)
    {
        if (loopCount <= 0 || runnerIndex < 0 || runnerIndex >= _runnerCompletedLoops.Length)
            return;

        int remaining;
        lock (_loopCountdownLock)
        {
            _runnerCompletedLoops[runnerIndex]++;
            var completedFullCycles = _runnerCompletedLoops.Min();
            remaining = Math.Max(0, loopCount - completedFullCycles);

            if (remaining == _remainingLoopCount)
                return;

            _remainingLoopCount = remaining;
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            SetPlaybackCounterText(LoopCountTextBox, remaining.ToString());
            if (remaining <= 0)
                StopAllRunners();
        }));
    }

    private void SetTimelineEditingEnabled(bool isEnabled)
    {
        _isTimelineEditingEnabled = isEnabled;

        if (!isEnabled)
            AddPopup.IsOpen = false;

        AddTimelineButton.IsEnabled = isEnabled;
        ClearButton.IsEnabled = isEnabled;
        UseStandardDelayCheckBox.IsEnabled = isEnabled;
        StandardDelayTextBox.IsEnabled = isEnabled;
        ShowKeyUpDownCheckBox.IsEnabled = isEnabled;
        PreviousTimelineOptionsButton.IsEnabled = isEnabled;
        NextTimelineOptionsButton.IsEnabled = isEnabled;
        AddMacroTabButton.IsEnabled = isEnabled;
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
}
