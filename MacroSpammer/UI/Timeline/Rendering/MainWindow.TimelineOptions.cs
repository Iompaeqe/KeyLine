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
        SetFormattedDelayInput(StandardDelayTextBox, StandardDelayUnitTextBlock, timeline.StandardDelayMs);
        ShowKeyUpDownCheckBox.IsChecked = timeline.ShowKeyUpDown;
        InputModeTextBlock.Text = timeline.UseTextInputMode ? "Text" : "Key";

        ShowKeyUpDownCheckBox.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Hidden;
        ShowKeyUpDownLeadingSeparator.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Hidden;
        ShowKeyUpDownCheckSeparator.Visibility = timeline.UseStandardDelay ? Visibility.Visible : Visibility.Hidden;

        ActiveTimelineTextBlock.Text = $"{timeline.Name}/{_document.Timelines.Count}";
        UpdateTimelineOptionsPagerVisibility();

        _isSyncingOptions = false;
    }

    private void UpdateTimelineOptionsPagerVisibility()
    {
        var hasMultipleTimelines = _document.Timelines.Count > 1;

        if (PreviousTimelineOptionsButton != null)
        {
            PreviousTimelineOptionsButton.Visibility = Visibility.Visible;
            PreviousTimelineOptionsButton.IsEnabled = hasMultipleTimelines;
        }

        if (NextTimelineOptionsButton != null)
        {
            NextTimelineOptionsButton.Visibility = Visibility.Visible;
            NextTimelineOptionsButton.IsEnabled = hasMultipleTimelines;
        }

        if (ActiveTimelineTextBlock != null)
            ActiveTimelineTextBlock.Visibility = Visibility.Visible;
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
        ApplyStandardDelayToActiveTimeline(GetStandardDelayMs());
        timeline.UseTextInputMode = InputModeTextBlock.Text == "Text";

        if (!timeline.UseStandardDelay)
            timeline.ShowKeyUpDown = true;
        else
            timeline.ShowKeyUpDown = ShowKeyUpDownCheckBox.IsChecked == true;
    }

    private void ApplyStandardDelayToActiveTimeline(int standardDelayMs)
    {
        _document.ActiveTimeline.StandardDelayMs = standardDelayMs;
        SetFormattedDelayInput(StandardDelayTextBox, StandardDelayUnitTextBlock, standardDelayMs);
    }

    private bool AreTimelineOptionControlsReady() =>
        UseStandardDelayCheckBox != null &&
        StandardDelayTextBox != null &&
        StandardDelayUnitTextBlock != null &&
        ShowKeyUpDownCheckBox != null &&
        ShowKeyUpDownLeadingSeparator != null &&
        ShowKeyUpDownCheckSeparator != null &&
        InputModeTextBlock != null &&
        PreviousInputModeButton != null &&
        NextInputModeButton != null &&
        ActiveTimelineTextBlock != null;

    private void InputModePagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSyncingOptions || !AreTimelineOptionControlsReady())
            return;

        InputModeTextBlock.Text = InputModeTextBlock.Text == "Text" ? "Key" : "Text";
        ApplyOptionsToActiveTimeline();
        ScheduleSaveState();
    }

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
