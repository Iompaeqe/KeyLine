using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer.UI.Nodes;

public partial class BackgroundMouseNode : NodeBase
{
    public event EventHandler? CoordinateCommitted;
    public event EventHandler? TargetPickRequested;

    public BackgroundMouseNode()
    {
        InitializeComponent();
    }

    protected override void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        ActionTextBlock.Text = step.Type switch
        {
            MacroNodeType.CursorMove => "MOVE",
            MacroNodeType.BackgroundMouseDown => "BG DOWN",
            MacroNodeType.BackgroundMouseUp => "BG UP",
            MacroNodeType.BackgroundMouseClick => "BG CLICK",
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
