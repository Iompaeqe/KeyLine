using System.Windows;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public partial class BlockNode : BlockNodeBase
{
    public BlockNode()
    {
        InitializeComponent();
    }

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        RootBorder.Width = IsBlockStart ? 10 : 8;
        RootBorder.ToolTip = GetTooltip(step);

        var lineColor = IsSelected
            ? Color.FromRgb(216, 180, 254)
            : Color.FromArgb(150, 168, 85, 247);

        BoundaryLine.Background = new SolidColorBrush(lineColor);
        BoundaryLine.Height = IsSelected ? 34 : 28;
        BoundaryLine.Width = IsSelected ? 4 : 3;
    }

    private static string GetTooltip(MacroNode step)
    {
        return step.Type switch
        {
            MacroNodeType.RepeatStart => "Repeat block start. Selects the full block.",
            MacroNodeType.RepeatEnd => "Repeat block end. Selects the full block.",
            MacroNodeType.ConditionStart => "Condition block start. Selects the full block.",
            MacroNodeType.ConditionEnd => "Condition block end. Selects the full block.",
            _ => "Block boundary. Selects the full block."
        };
    }
}
