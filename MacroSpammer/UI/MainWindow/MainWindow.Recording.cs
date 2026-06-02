using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void StartRecording(MacroTimeline timeline)
    {
        SelectTimeline(timeline);

        _recordingTimeline = timeline;
        _recorder.Start();

        RecordStopButtonHost.IsHitTestVisible = true;
        RecordStopButtonHost.Visibility = Visibility.Visible;
        RecordingMouseNotice.Visibility = Visibility.Visible;
        StatusText.Text = $"● Recording {timeline.Name}";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));

        Focus();
    }

    private void StopRecording()
    {
        var timeline = _recordingTimeline;

        _recorder.Stop();
        _recordingTimeline = null;

        RecordStopButtonHost.Visibility = Visibility.Collapsed;
        RecordStopButtonHost.IsHitTestVisible = false;
        RecordingMouseNotice.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));

        if (timeline != null && MergeAdjacentDelayNodesIfEnabled(timeline))
            ScheduleSaveState();

        RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void RecordMouseDown(int mouseButton)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordMouseDown(mouseButton, timeline.Nodes.Count > 0).ToList();

        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void RecordMouseUp(int mouseButton)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordMouseUp(mouseButton, timeline.Nodes.Count > 0).ToList();

        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void AppendRecordedInputSteps(MacroTimeline timeline, List<MacroNode> addedSteps)
    {
        if (addedSteps.Count == 0)
            return;

        SaveUndoSnapshot();

        foreach (var step in addedSteps)
            timeline.Nodes.Add(step);

        AppendRecordedStepsToTimelineRow(timeline, addedSteps);

        ScrollToTimelineEndAfterRecordingAppend();

        ScheduleSaveState();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            CancelTimelineDragState();

        if (IsShortcutCaptureActive())
            return;

        if (!_recorder.IsRecording && TryHandleEditingShortcut(e))
            return;

        if (!_recorder.IsRecording && e.Key == Key.Delete)
        {
            DeleteSelectedItem();
            e.Handled = true;
            return;
        }

        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyDown(e, timeline.Nodes.Count > 0).ToList();

        e.Handled = true;
        AppendRecordedInputSteps(timeline, addedSteps);
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedSteps = _recorder.RecordKeyUp(e, timeline.Nodes.Count > 0).ToList();

        e.Handled = true;
        AppendRecordedInputSteps(timeline, addedSteps);
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
