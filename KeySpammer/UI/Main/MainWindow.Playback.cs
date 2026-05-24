using System.Windows;
using System.Windows.Media;
using KeySpammer.Services.Playback;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow
{
    private int GetStandardDelayMs() =>
        int.TryParse(StandardDelayTextBox.Text, out var ms) ? Math.Max(0, ms) : 50;

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CancelTimelineDragState();
        
        var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;

        timeline.Steps.Clear();
        _selection.Clear();

        SelectTimeline(timeline);
        RefreshTimeline();
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
        {
            StopAllRunners();
            SetStoppedStatus();
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

        StartStopButton.Content = "■  Stop";
        StatusText.Text = $"Running {runnableTimelines.Count} timeline(s)";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

        var loopCount = int.TryParse(LoopCountTextBox.Text, out var loops) ? loops : 0;

        var tasks = new List<Task>();

        foreach (var timeline in runnableTimelines)
        {
            var runner = GetRunner(timeline);

            tasks.Add(runner.StartAsync(
                target.Handle,
                timeline.Steps.ToList(),
                loopCount,
                timeline.UseStandardDelay,
                timeline.StandardDelayMs));
        }

        await Task.WhenAll(tasks);

        SetStoppedStatus();
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

    private void SetStoppedStatus()
    {
        StartStopButton.Content = "▶  Start";
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
    }
}
