using KeySpammer.Domain;
using KeySpammer.State;

namespace KeySpammer;

public partial class MainWindow
{
    private void DeleteSelectedItem()
    {
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
        if (_runners.TryGetValue(timeline, out var runner))
            runner.Stop();

        _runners.Remove(timeline);

        _document.RemoveTimeline(timeline);
        _selection.Clear();

        SelectTimeline(_document.ActiveTimeline);
        RefreshTimeline();
    }

    private void DeleteSelectedStep()
    {
        var timeline = _selection.SelectedTimeline;
        var selectedStep = _selection.SelectedStep;

        if (timeline == null || selectedStep == null)
            return;

        if (selectedStep.IsSyntheticDisplayStep)
        {
            foreach (var sourceStep in selectedStep.SourceSteps.ToList())
                timeline.Steps.Remove(sourceStep);
        }
        else
        {
            timeline.Steps.Remove(selectedStep);
        }

        _selection.Clear();
        RefreshTimeline();
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
    }
}