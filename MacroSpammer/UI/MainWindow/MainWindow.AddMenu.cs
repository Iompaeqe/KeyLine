using System.Windows;
using System.Windows.Controls.Primitives;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void AddTimelineButton_Click(object sender, RoutedEventArgs e)
    {
        SaveUndoSnapshot();
        var timeline = _document.AddTimeline();
        ApplyDefaultSettingsToTimeline(timeline, _settings);
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
        UpdateExperimentalAddMenuVisibility();
        AddPopup.IsOpen = true;
    }

    private void UpdateExperimentalAddMenuVisibility()
    {
        if (ExperimentalAddMenuExpander != null)
            ExperimentalAddMenuExpander.Visibility = _settings.ExperimentalFeaturesEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
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

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.Delay,
            DelayMs = 100,
            IsRecordedDelay = false
        });

        timeline.UseStandardDelay = false;
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void RandomDelayMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.RandomDelay,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150,
            IsRecordedDelay = false
        });

        timeline.UseStandardDelay = false;
        MergeAdjacentDelayNodesIfEnabled(timeline);
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

        SaveUndoSnapshot();
        timeline.Steps.Add(new MacroStep
        {
            Type = MacroStepType.Text,
            Text = dialog.ResultText
        });

        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void CursorMoveMenuButton_Click(object sender, RoutedEventArgs e)
    {
        AddPopup.IsOpen = false;

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        var step = new MacroStep
        {
            Type = MacroStepType.CursorMove,
            MouseX = 0,
            MouseY = 0
        };

        timeline.Steps.Add(step);
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
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

        SaveUndoSnapshot();
        var timeline = GetPopupTimeline();
        timeline.Steps.Add(new MacroStep
        {
            Type = type,
            MouseX = 0,
            MouseY = 0
        });

        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

}
