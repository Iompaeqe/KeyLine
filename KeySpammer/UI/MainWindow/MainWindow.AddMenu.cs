using System.Windows;
using System.Windows.Controls.Primitives;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private void AddTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        var timeline = _document.AddTimeline();
        SelectTimeline(timeline);
        RefreshTimeline();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        _popupTimeline = ResolveTimelineFromSender(sender) ?? _document.ActiveTimeline;

        if (sender is UIElement placementTarget)
            AddPopup.PlacementTarget = placementTarget;

        AddPopup.Placement = PlacementMode.Top;
        AddPopup.IsOpen = true;
    }

    private MacroTimeline? ResolveTimelineFromSender(object sender)
    {
        if (sender is FrameworkElement fe && fe.Tag is MacroTimeline timeline)
            return timeline;

        return null;
    }

    private MacroTimeline GetPopupTimeline()
    {
        return _popupTimeline ?? _document.ActiveTimeline;
    }

    private void RecordMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;
        StartRecording(GetPopupTimeline());
    }

    private void DelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.Delay,
            DelayMs = 100,
            IsRecordedDelay = false
        });

        timeline.UseStandardDelay = false;
        SelectTimeline(timeline);
        RefreshTimeline();
    }

    private void TextMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        var dialog = new TextInputWindow { Owner = this };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.Text,
            Text = dialog.ResultText
        });

        SelectTimeline(timeline);
        RefreshTimeline();
    }
}
