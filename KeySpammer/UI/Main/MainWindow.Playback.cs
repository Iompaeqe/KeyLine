using System.Windows;
using System.Windows.Media;

namespace KeySpammer;

public partial class MainWindow
{
    private int GetStandardDelayMs() =>
        int.TryParse(StandardDelayTextBox.Text, out var ms) ? Math.Max(0, ms) : 50;

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _document.Steps.Clear();
        _selection.Clear();
        RefreshTimeline();
    }

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner.IsRunning)
        {
            _runner.Stop();
            StartStopButton.Content = "▶  Start";
            StatusText.Text = "Stopped";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
            return;
        }

        var target = GetTargetHandle();
        if (target == null || _document.Steps.Count == 0)
            return;

        StartStopButton.Content = "■  Stop";
        StatusText.Text = "Running";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));

        var loopCount = int.TryParse(LoopCountTextBox.Text, out var loops) ? loops : 0;

        await _runner.StartAsync(
            target.Handle,
            _document.Steps.ToList(),
            loopCount,
            UseStandardDelayCheckBox.IsChecked == true,
            GetStandardDelayMs());

        StartStopButton.Content = "▶  Start";
        StatusText.Text = "Stopped";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(61, 84, 112));
    }
}
