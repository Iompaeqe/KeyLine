using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.State;

namespace KeySpammer;

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
        
        CancelTimelineDragState();
        
        if (!_recorder.IsRecording && e.Key == Key.Delete)
        {
            DeleteSelectedItem();
            e.Handled = true;
            return;
        }

        if (!_recorder.IsRecording)
            return;

        if (e.Key == Key.Escape)
        {
            StopRecording();
            e.Handled = true;
            return;
        }

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;

        foreach (var step in _recorder.RecordKeyDown(e, timeline.Steps.Count > 0))
            timeline.Steps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;

        foreach (var step in _recorder.RecordKeyUp(e, timeline.Steps.Count > 0))
            timeline.Steps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }
}
