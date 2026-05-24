using System.Windows;
using System.Windows.Input;
using KeySpammer.Domain;
using KeySpammer.UI.Config;

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
        StatusText.Foreground = UiBrushes.Get(System.Windows.Media.Color.FromRgb(248, 113, 113));

        Focus();
    }

    private void StopRecording()
    {
        _recorder.Stop();
        _recordingTimeline = null;

        RecordStopButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = UiBrushes.Get(System.Windows.Media.Color.FromRgb(61, 84, 112));

        if (_document.ActiveTimeline != null)
            RefreshTimelineRow(_document.ActiveTimeline);
        else
            RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
        {
            CancelTimelineDragState();

            if (e.Key == Key.Delete)
            {
                DeleteSelectedItem();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Escape)
        {
            StopRecording();
            e.Handled = true;
            return;
        }

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedAny = false;

        foreach (var step in _recorder.RecordKeyDown(e, timeline.Steps.Count > 0))
        {
            timeline.Steps.Add(step);
            addedAny = true;
        }

        e.Handled = true;

        if (addedAny)
            RefreshTimelineRow(timeline);
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        var timeline = _recordingTimeline ?? _document.ActiveTimeline;
        var addedAny = false;

        foreach (var step in _recorder.RecordKeyUp(e, timeline.Steps.Count > 0))
        {
            timeline.Steps.Add(step);
            addedAny = true;
        }

        e.Handled = true;

        if (addedAny)
            RefreshTimelineRow(timeline);
    }
}
