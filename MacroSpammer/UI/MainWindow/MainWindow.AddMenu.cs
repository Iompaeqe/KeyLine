using System.Windows;
using System.Windows.Controls.Primitives;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void AddTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        var timeline = _document.AddTimeline();
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
            return;

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
        ScheduleSaveState();
    }

    private void RandomDelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.RandomDelay,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            IsRecordedDelay = false
        });

        timeline.UseStandardDelay = false;
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
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
        ScheduleSaveState();
    }

    private async void CursorMoveMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        var step = new MacroStep
        {
            Type = MacroStepType.CursorMove,
            MouseX = 0,
            MouseY = 0
        };

        timeline.Steps.Add(step);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();

        await PickMouseCoordinatesForStepAsync(step);
    }

    private void MouseDownMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroStepType.MouseDown);

    private void MouseUpMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroStepType.MouseUp);

    private void MouseClickMenuButton_Click(object sender, RoutedEventArgs e) =>
        AddMouseStep(MacroStepType.MouseClick);

    private void AddMouseStep(MacroStepType type)
    {
        AddPopup.IsOpen = false;

        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = type,
            MouseX = 0,
            MouseY = 0
        });

        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

}
