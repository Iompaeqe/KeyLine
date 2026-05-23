using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KeySpammer;

public partial class MainWindow
{
    private void StartRecording()
    {
        _recorder.Start();
        RecordStopButton.Visibility = Visibility.Visible;
        StatusText.Text = "● Recording";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        Focus();
    }

    private void StopRecording()
    {
        _recorder.Stop();
        RecordStopButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
        RefreshTimeline();
    }

    private void RecordStopButton_Click(object sender, RoutedEventArgs e) => StopRecording();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording && e.Key == Key.Delete && _selection.SelectedStep != null)
        {
            DeleteSelectedStep();
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

        foreach (var step in _recorder.RecordKeyDown(e, _document.Steps.Count > 0))
            _document.Steps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!_recorder.IsRecording)
            return;

        foreach (var step in _recorder.RecordKeyUp(e, _document.Steps.Count > 0))
            _document.Steps.Add(step);

        e.Handled = true;
        RefreshTimeline();
    }
}
