using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public partial class MouseNode : NodeBase
{
    public MouseNode()
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
            MacroNodeType.MouseClick => "CLICK",
            MacroNodeType.MouseScrollUp => "SCROLL UP",
            MacroNodeType.MouseScrollDown => "SCROLL DN",
            MacroNodeType.MouseDown => $"M{NormalizeMouseButton(step.MouseButton)}↓",
            MacroNodeType.MouseUp => $"M{NormalizeMouseButton(step.MouseButton)}↑",
            _ => "M"
        };

        RootBorder.Background = new SolidColorBrush(IsSelected
            ? Color.FromRgb(37, 70, 72)
            : Color.FromRgb(22, 47, 50));
        RootBorder.BorderBrush = new SolidColorBrush(IsSelected
            ? Color.FromRgb(45, 212, 191)
            : Color.FromRgb(20, 130, 118));
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        ActionTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(204, 251, 241)
            : Color.FromRgb(153, 246, 228));
    }

    private static int NormalizeMouseButton(int mouseButton) => Math.Clamp(mouseButton, 1, 5);
}
