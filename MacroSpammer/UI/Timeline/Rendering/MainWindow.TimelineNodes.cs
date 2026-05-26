using System.Windows;
using System.Windows.Controls;
using MacroSpammer.Domain;
using MacroSpammer.UI.Controls;

namespace MacroSpammer;

public partial class MainWindow
{
    private UIElement CreateStepBlock(MacroTimeline timeline, MacroStep step)
    {
        return step.Type switch
        {
            MacroStepType.Delay or MacroStepType.RandomDelay => CreateDelayBlock(timeline, step),
            MacroStepType.Text => CreateTextStepBlock(timeline, step),
            MacroStepType.ForegroundMouseDown or MacroStepType.ForegroundMouseUp => CreateKeyStepBlock(timeline, step),
            MacroStepType.ForegroundMouseClick => CreateForegroundMouseStepBlock(timeline, step),
            MacroStepType.CursorMove or MacroStepType.MouseDown or MacroStepType.MouseUp or MacroStepType.MouseClick => CreateMouseStepBlock(timeline, step),
            MacroStepType.KeyDown or MacroStepType.KeyUp => CreateKeyStepBlock(timeline, step),
            _ => CreateTextStepBlock(timeline, step)
        };
    }

    private UIElement CreateKeyStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new KeyStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            Tag = step
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateTextStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new TextStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateDelayBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new DelayStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        control.DelayCommitted += (_, _) =>
        {
            RefreshTimeline();
            ScheduleSaveState();
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateMouseStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new MouseStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        control.CoordinateCommitted += (_, _) =>
        {
            RefreshTimeline();
            ScheduleSaveState();
        };

        control.TargetPickRequested += async (_, _) =>
        {
            SelectTimeline(timeline);
            _selection.SelectStep(timeline, step);
            await PickMouseCoordinatesForStepAsync(step);
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateForegroundMouseStepBlock(MacroTimeline timeline, MacroStep step)
    {
        var control = new ForegroundMouseStepControl
        {
            Step = step,
            IsSelected = IsStepSelected(timeline, step),
            Tag = step
        };

        AttachStepMouseHandlers(control, timeline, step);
        return control;
    }

    private UIElement CreateAddBlock(MacroTimeline timeline)
    {
        var control = new AddStepControl
        {
            Tag = timeline
        };

        control.AddClicked += AddButton_Click;
        return control;
    }

    private bool IsStepSelected(MacroTimeline timeline, MacroStep step)
    {
        if (!_selection.HasStepSelection || _selection.SelectedTimeline == null || _selection.SelectedStep == null)
            return false;

        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
            return false;

        if (ReferenceEquals(step, _selection.SelectedStep))
            return true;

        if (step.IsSyntheticDisplayStep)
        {
            return step.SourceSteps.Contains(_selection.SelectedStep) ||
                   (_selection.SelectedStep.IsSyntheticDisplayStep &&
                    step.SourceSteps.SequenceEqual(_selection.SelectedStep.SourceSteps));
        }

        if (_selection.SelectedStep.IsSyntheticDisplayStep)
            return _selection.SelectedStep.SourceSteps.Contains(step);

        return false;
    }

    private static Size MeasureTimelineItem(UIElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var width = element.DesiredSize.Width;
        var height = element.DesiredSize.Height;

        if (element is FrameworkElement frameworkElement)
        {
            if (!double.IsNaN(frameworkElement.Width) && frameworkElement.Width > 0)
                width = frameworkElement.Width;

            if (!double.IsNaN(frameworkElement.Height) && frameworkElement.Height > 0)
                height = frameworkElement.Height;
        }

        if (width <= 0)
            width = 72;

        if (height <= 0)
            height = 48;

        return new Size(width, height);
    }
}
