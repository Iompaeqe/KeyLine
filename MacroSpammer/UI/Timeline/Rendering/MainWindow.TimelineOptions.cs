using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void SelectTimeline(MacroTimeline timeline)
    {
        if (_isClearConfirmationActive && !ReferenceEquals(_pendingClearTimeline, timeline))
            ResetClearConfirmation();

        _document.SelectTimeline(timeline);
        SyncOptionsFromActiveTimeline();
        ScheduleSaveState();
    }

    private void SyncOptionsFromActiveTimeline()
    {
        if (!AreTimelineOptionControlsReady())
            return;

        var timeline = _document.ActiveTimeline;

        _isSyncingOptions = true;

        UseStandardDelayCheckBox.IsChecked = timeline.UseStandardDelay;
        StandardDelayTextBox.Text = timeline.StandardDelayMs.ToString();
        ShowKeyUpDownCheckBox.IsChecked = timeline.ShowKeyUpDown;
        TextInputModeCheckBox.IsChecked = timeline.UseTextInputMode;

        ShowKeyUpDownCheckBox.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        ShowKeyUpDownCheckSeparator.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Collapsed;

        ActiveTimelineTextBlock.Text = $"{timeline.Name}/{_document.Timelines.Count}";
        UpdateTimelineOptionsPagerVisibility();

        _isSyncingOptions = false;
    }

    private void UpdateTimelineOptionsPagerVisibility()
    {
        var visibility = _document.Timelines.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (PreviousTimelineOptionsButton != null)
            PreviousTimelineOptionsButton.Visibility = visibility;

        if (NextTimelineOptionsButton != null)
            NextTimelineOptionsButton.Visibility = visibility;

        if (ActiveTimelineTextBlock != null)
            ActiveTimelineTextBlock.Visibility = visibility;
    }

    private void OptionsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (_isSyncingOptions || !AreTimelineOptionControlsReady())
            return;

        ApplyOptionsToActiveTimeline();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void StandardDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingOptions || !AreTimelineOptionControlsReady())
            return;

        ApplyOptionsToActiveTimeline();
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void ApplyOptionsToActiveTimeline()
    {
        if (!AreTimelineOptionControlsReady())
            return;

        var timeline = _document.ActiveTimeline;

        timeline.UseStandardDelay = UseStandardDelayCheckBox.IsChecked == true;
        timeline.StandardDelayMs = GetStandardDelayMs();
        timeline.UseTextInputMode = TextInputModeCheckBox.IsChecked == true;

        if (!timeline.UseStandardDelay)
            timeline.ShowKeyUpDown = true;
        else
            timeline.ShowKeyUpDown = ShowKeyUpDownCheckBox.IsChecked == true;
    }

    private bool AreTimelineOptionControlsReady() =>
        UseStandardDelayCheckBox != null &&
        StandardDelayTextBox != null &&
        ShowKeyUpDownCheckBox != null &&
        ShowKeyUpDownCheckSeparator != null &&
        TextInputModeCheckBox != null &&
        ActiveTimelineTextBlock != null;

    private void PreviousTimelineOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _document.SelectPreviousTimeline();
        _selection.SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void NextTimelineOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        _document.SelectNextTimeline();
        _selection.SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
        ScheduleSaveState();
    }
}
