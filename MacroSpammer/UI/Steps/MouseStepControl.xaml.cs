using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer.UI.Steps;

public partial class MouseStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;

    public event EventHandler? CoordinateCommitted;
    public event EventHandler? TargetPickRequested;

    public MacroStep? Step
    {
        get => _step;
        set
        {
            _step = value;
            Tag = value;
            UpdateVisual();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            UpdateVisual();
        }
    }

    public MouseStepControl()
    {
        InitializeComponent();
    }

    public bool IsEditorSource(DependencyObject? source) => false;

    private void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        ActionTextBlock.Text = step.Type switch
        {
            MacroStepType.CursorMove => "MOVE",
            MacroStepType.MouseDown => "BG DOWN",
            MacroStepType.MouseUp => "BG UP",
            MacroStepType.MouseClick => "BG CLICK",
            _ => "MOUSE"
        };

        CoordinateTextBlock.Text = $"{step.MouseX}, {step.MouseY}";

        RootBorder.Background = new SolidColorBrush(IsSelected
            ? Color.FromRgb(48, 34, 84)
            : Color.FromRgb(30, 25, 46));
        RootBorder.BorderBrush = new SolidColorBrush(IsSelected
            ? Color.FromRgb(168, 85, 247)
            : Color.FromRgb(86, 66, 120));
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        ActionTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(243, 232, 255)
            : Color.FromRgb(216, 180, 254));
        CoordinateTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(233, 213, 255)
            : Color.FromRgb(196, 181, 253));
    }

    protected virtual void OnCoordinateCommitted() => CoordinateCommitted?.Invoke(this, EventArgs.Empty);

    protected virtual void OnTargetPickRequested() => TargetPickRequested?.Invoke(this, EventArgs.Empty);
}
