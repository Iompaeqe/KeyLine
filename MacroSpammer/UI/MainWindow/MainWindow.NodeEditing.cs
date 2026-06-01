using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private void DeleteSelectedItem()
    {
        if (_runners.Values.Any(runner => runner.IsRunning))
            return;

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

        SaveUndoSnapshot();
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

        if (timeline == null || _selection.SelectedStep == null)
            return;

        if (!_selection.HasMultipleStepSelection)
        {
            DeleteStep(timeline, _selection.SelectedStep);
            return;
        }

        DeleteSteps(timeline, _selection.SelectedSteps.ToList());
    }

    private void DeleteStep(MacroTimeline timeline, MacroNode node)
    {
        ResetTimelineDeleteConfirmation();

        SaveUndoSnapshot();
        var stepsToRemove = GetStepsToRemoveForDelete(timeline, node);
        if (timeline.UseStandardDelay && stepsToRemove.Any(IsDelayCleanupActionStep))
            AddStandardDelayCleanupSteps(timeline, stepsToRemove);

        foreach (var stepToRemove in stepsToRemove)
            timeline.Steps.Remove(stepToRemove);

        MergeAdjacentDelayNodesIfEnabled(timeline);
        _selection.Clear();
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void DeleteSteps(MacroTimeline timeline, IReadOnlyList<MacroNode> steps)
    {
        ResetTimelineDeleteConfirmation();

        var stepsToRemove = new List<MacroNode>();
        foreach (var step in steps)
        {
            var rawSteps = GetStepsToRemoveForDelete(timeline, step);
            if (timeline.UseStandardDelay && rawSteps.Any(IsDelayCleanupActionStep))
                AddStandardDelayCleanupSteps(timeline, rawSteps);

            foreach (var rawStep in rawSteps)
            {
                if (!stepsToRemove.Contains(rawStep))
                    stepsToRemove.Add(rawStep);
            }
        }

        if (stepsToRemove.Count == 0)
            return;

        SaveUndoSnapshot();
        foreach (var stepToRemove in stepsToRemove.OrderByDescending(timeline.Steps.IndexOf))
            timeline.Steps.Remove(stepToRemove);

        MergeAdjacentDelayNodesIfEnabled(timeline);
        _selection.Clear();
        SelectTimeline(timeline);
        RefreshTimeline();
        ScheduleSaveState();
    }

    private static List<MacroNode> GetStepsToRemoveForDelete(MacroTimeline timeline, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(timeline.Steps.Contains)
                .ToList();
        }

        return timeline.Steps.Contains(node)
            ? new List<MacroNode> { node }
            : new List<MacroNode>();
    }

    private static void AddStandardDelayCleanupSteps(MacroTimeline timeline, List<MacroNode> stepsToRemove)
    {
        if (stepsToRemove.Count == 0)
            return;

        var indexes = stepsToRemove
            .Select(timeline.Steps.IndexOf)
            .Where(index => index >= 0)
            .Order()
            .ToList();

        if (indexes.Count == 0)
            return;

        var cleanupSteps = GetContiguousDelayStepsBefore(timeline, indexes[0]);
        if (cleanupSteps.Count == 0)
            cleanupSteps = GetContiguousDelayStepsAfter(timeline, indexes[^1]);

        foreach (var cleanupStep in cleanupSteps)
        {
            if (!stepsToRemove.Contains(cleanupStep))
                stepsToRemove.Add(cleanupStep);
        }
    }

    private static List<MacroNode> GetContiguousDelayStepsBefore(MacroTimeline timeline, int stepIndex)
    {
        var result = new List<MacroNode>();

        for (var i = stepIndex - 1; i >= 0 && IsDelayCleanupStep(timeline.Steps[i]); i--)
            result.Add(timeline.Steps[i]);

        return result;
    }

    private static List<MacroNode> GetContiguousDelayStepsAfter(MacroTimeline timeline, int stepIndex)
    {
        var result = new List<MacroNode>();

        for (var i = stepIndex + 1; i < timeline.Steps.Count && IsDelayCleanupStep(timeline.Steps[i]); i++)
            result.Add(timeline.Steps[i]);

        return result;
    }

    private static bool IsDelayCleanupStep(MacroNode node) =>
        node.Type is MacroNodeType.Delay or MacroNodeType.RandomDelay;

    private static bool IsDelayCleanupActionStep(MacroNode node) =>
        !IsDelayCleanupStep(node);

    private void EditTextStep(MacroTimeline timeline, MacroNode node)
    {
        var dialog = new TextInputWindow { Owner = this };
        dialog.SetText(node.Text);

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        SaveUndoSnapshot();
        node.Text = dialog.ResultText;
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

        SaveUndoSnapshot();
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
