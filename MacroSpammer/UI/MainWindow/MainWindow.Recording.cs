using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.State;

namespace MacroSpammer;

public partial class MainWindow
{
    private void StartRecording(MacroTimeline timeline)
    {
        SelectTimeline(timeline);

        _recordingTimeline = timeline;
        _recorder.Start();

        RecordStopButton.Visibility = Visibility.Visible;
        StatusText.Text = $"● Recording {timeline.Name}";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));

        Focus();
    }

    private void StopRecording()
    {
        _recorder.Stop();
        _recordingTimeline = null;

        RecordStopButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));

        RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            CancelTimelineDragState();

        if (!_recorder.IsRecording && e.Key == Key.Delete)
        {
            DeleteSelectedItem();
            e.Handled = true;
            return;
        }

        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyDown(e, timeline.Steps.Count > 0).ToList();

        foreach (var step in addedSteps)
            timeline.Steps.Add(step);

        e.Handled = true;
        AppendRecordedStepsToTimelineRow(timeline, addedSteps);
        ScrollToTimelineEndAfterRecordingAppend();

        if (addedSteps.Count > 0)
            ScheduleSaveState();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyUp(e, timeline.Steps.Count > 0).ToList();

        foreach (var step in addedSteps)
            timeline.Steps.Add(step);

        e.Handled = true;
        AppendRecordedStepsToTimelineRow(timeline, addedSteps);
        ScrollToTimelineEndAfterRecordingAppend();

        if (addedSteps.Count > 0)
            ScheduleSaveState();
    }

    private void ScrollToTimelineEndAfterRecordingAppend()
    {
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                TimelineScrollViewer.ScrollToRightEnd();
                UpdateTimelineScrollIndicator();
            }));
    }
}
