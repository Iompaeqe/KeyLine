
using KeySpammer.Domain;

namespace KeySpammer;

public partial class MainWindow
{
    private void DeleteSelectedStep()
    {
        if (!_selection.HasSelection || _selection.SelectedStep == null)
            return;

        if (_selection.SelectedStep.IsSyntheticDisplayStep)
        {
            foreach (var sourceStep in _selection.SelectedStep.SourceSteps.ToList())
                _document.Steps.Remove(sourceStep);
        }
        else
        {
            _document.Steps.Remove(_selection.SelectedStep);
        }

        _selection.Clear();
        RefreshTimeline();
    }

    private void EditTextStep(MacroStep step)
    {
        var dialog = new TextInputWindow { Owner = this };
        dialog.SetText(step.Text);

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
            return;

        step.Text = dialog.ResultText;
        RefreshTimeline();
    }
}
