using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

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
        var step = Node;
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

        var palette = step.Type == MacroNodeType.CursorMove
            ? MousePalette
            : ExperimentalPalette;

        RootBorder.Background = new SolidColorBrush(IsSelected
            ? palette.BackgroundSelected
            : palette.Background);
        RootBorder.BorderBrush = new SolidColorBrush(IsSelected
            ? palette.BorderSelected
            : palette.Border);
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        ActionTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? palette.ActionSelected
            : palette.Action);
        CoordinateTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? palette.DetailSelected
            : palette.Detail);
    }

    protected virtual void OnCoordinateCommitted() => CoordinateCommitted?.Invoke(this, EventArgs.Empty);

    protected virtual void OnTargetPickRequested() => TargetPickRequested?.Invoke(this, EventArgs.Empty);

    private static readonly NodePalette MousePalette = new(
        Color.FromRgb(22, 47, 50),
        Color.FromRgb(37, 70, 72),
        Color.FromRgb(20, 130, 118),
        Color.FromRgb(45, 212, 191),
        Color.FromRgb(153, 246, 228),
        Color.FromRgb(204, 251, 241),
        Color.FromRgb(94, 234, 212),
        Color.FromRgb(204, 251, 241));

    private static readonly NodePalette ExperimentalPalette = new(
        Color.FromRgb(58, 10, 37),
        Color.FromRgb(87, 18, 56),
        Color.FromRgb(190, 24, 93),
        Color.FromRgb(244, 114, 182),
        Color.FromRgb(251, 207, 232),
        Color.FromRgb(252, 231, 243),
        Color.FromRgb(249, 168, 212),
        Color.FromRgb(252, 231, 243));

    private sealed record NodePalette(
        Color Background,
        Color BackgroundSelected,
        Color Border,
        Color BorderSelected,
        Color Action,
        Color ActionSelected,
        Color Detail,
        Color DetailSelected);
}
