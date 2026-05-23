using System.Windows;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;
using KeySpammer.UI.Controls;

namespace KeySpammer;

public partial class MainWindow
{
    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        if (TimelinePanel == null)
            return;

        var useStandardDelay = UseStandardDelayCheckBox?.IsChecked == true;

        ShowKeyUpDownCheckBox.Visibility = useStandardDelay ? Visibility.Visible : Visibility.Collapsed;
        ShowKeyUpDownCheckSeparator.Visibility = useStandardDelay ? Visibility.Visible : Visibility.Collapsed;

        if (!useStandardDelay)
            ShowKeyUpDownCheckBox.IsChecked = true;

        TimelinePanel.Children.Clear();

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            _document.Steps.ToList(),
            useStandardDelay,
            ShowKeyUpDownCheckBox.IsChecked == true);

        foreach (var step in visibleSteps)
            TimelinePanel.Children.Add(CreateStepBlock(step));

        TimelinePanel.Children.Add(CreateAddBlock());

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private UIElement CreateStepBlock(MacroStep step)
    {
        return step.Type switch
        {
            MacroStepType.Delay => CreateDelayBlock(step),
            MacroStepType.Text => CreateTextStepBlock(step),
            MacroStepType.KeyDown or MacroStepType.KeyUp => CreateKeyStepBlock(step),
            _ => CreateTextStepBlock(step)
        };
    }

    private UIElement CreateKeyStepBlock(MacroStep step)
    {
        var control = new KeyStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(step),
            ShowKeyUpDown = ShowKeyUpDownCheckBox?.IsChecked == true
        };

        AttachStepMouseHandlers(control, step);
        return control;
    }

    private UIElement CreateTextStepBlock(MacroStep step)
    {
        var control = new TextStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(step)
        };

        AttachStepMouseHandlers(control, step);

        control.MouseRightButtonDown += (_, e) =>
        {
            EditTextStep(step);
            e.Handled = true;
        };

        return control;
    }

    private UIElement CreateDelayBlock(MacroStep step)
    {
        var control = new DelayStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(step)
        };

        control.DelayCommitted += (_, _) => RefreshTimeline();

        AttachStepMouseHandlers(control, step);
        return control;
    }

    private UIElement CreateAddBlock()
    {
        var control = new AddStepControl();
        control.AddClicked += AddButton_Click;
        return control;
    }

    private bool IsStepSelected(MacroStep step)
    {
        if (!_selection.HasSelection)
            return false;

        if (ReferenceEquals(step, _selection.SelectedStep))
            return true;

        if (step.IsSyntheticDisplayStep)
        {
            return _selection.SelectedStep != null &&
                   (step.SourceSteps.Contains(_selection.SelectedStep) ||
                    (_selection.SelectedStep.IsSyntheticDisplayStep &&
                     step.SourceSteps.SequenceEqual(_selection.SelectedStep.SourceSteps)));
        }

        if (_selection.SelectedStep != null && _selection.SelectedStep.IsSyntheticDisplayStep)
            return _selection.SelectedStep.SourceSteps.Contains(step);

        return false;
    }
}
