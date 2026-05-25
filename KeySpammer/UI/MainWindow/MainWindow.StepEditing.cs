using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private void DeleteSelectedItem()
    {
        ResetClearConfirmation();
        ResetTimelineDeleteConfirmation();

        if (_selection.HasStepSelection)
        {
            DeleteSelectedStep();
            return;
        }

        if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
            DeleteSelectedTimeline(_selection.SelectedTimeline);
    }

    private void DeleteSelectedTimeline(MacroTimeline timeline)
    {
        ResetTimelineDeleteConfirmation();

        if (_runners.TryGetValue(timeline, out var runner))
            runner.Stop();

        _runners.Remove(timeline);

        _document.RemoveTimeline(timeline);
        _selection.Clear();

        if (_document.Timelines.Count > 0)
            SelectTimeline(_document.ActiveTimeline);

        RefreshTimeline();
        ScheduleSaveState();
    }

    private void DeleteSelectedStep()
    {
        var timeline = _selection.SelectedTimeline;
        var selectedStep = _selection.SelectedStep;

        if (timeline == null || selectedStep == null)
            return;

        DeleteStep(timeline, selectedStep);
    }

    private void DeleteStep(MacroTimeline timeline, MacroStep step)
    {
        ResetTimelineDeleteConfirmation();

        if (step.IsSyntheticDisplayStep)
        {
            foreach (var sourceStep in step.SourceSteps.ToList())
                timeline.Steps.Remove(sourceStep);
        }
        else
        {
            timeline.Steps.Remove(step);
        }

        _selection.Clear();
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void EditTextStep(MacroTimeline timeline, MacroStep step)
    {
        var dialog = new TextInputWindow { Owner = this };
        dialog.SetText(step.Text);

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        step.Text = dialog.ResultText;
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }
    
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CancelTimelineDragState();

        if (!_isClearConfirmationActive)
        {
            BeginClearConfirmation(_document.ActiveTimeline);
            return;
        }

        if (_pendingClearTimeline == null)
        {
            ResetClearConfirmation();
            return;
        }

        ClearTimeline(_pendingClearTimeline);
        ResetClearConfirmation();
    }
    

    private void BeginClearConfirmation(MacroTimeline timeline)
    {
        _pendingClearTimeline = timeline;
        _isClearConfirmationActive = true;

        var confirmText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        confirmText.Inlines.Add(new Run(timeline.Name)
        {
            FontWeight = FontWeights.Black,
            FontSize = 14
        });

        confirmText.Inlines.Add(new Run(" - Confirm")
        {
            FontWeight = FontWeights.SemiBold,
            FontSize = 12
        });

        ClearButton.Content = confirmText;
        ClearButton.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
        ClearButton.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        ClearButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
    }

    private void ResetClearConfirmation()
    {
        _pendingClearTimeline = null;
        _isClearConfirmationActive = false;

        ClearButton.Content = "Clear";

        ClearButton.ClearValue(BackgroundProperty);
        ClearButton.ClearValue(BorderBrushProperty);
        ClearButton.ClearValue(ForegroundProperty);
    }

    private void ClearTimeline(MacroTimeline timeline)
    {
        ResetTimelineDeleteConfirmation();

        if (_runners.TryGetValue(timeline, out var runner))
            runner.Stop();

        if (ReferenceEquals(_recordingTimeline, timeline))
            StopRecording();

        timeline.Steps.Clear();

        if (ReferenceEquals(_selection.SelectedTimeline, timeline))
            _selection.Clear();

        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }
}
