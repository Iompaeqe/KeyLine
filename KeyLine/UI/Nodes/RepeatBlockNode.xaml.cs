using System.Windows;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public partial class RepeatBlockNode : BlockNodeBase
{
    public RepeatBlockNode()
    {
        InitializeComponent();
    }

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        RootBorder.Width = step.Type == MacroNodeType.RepeatStart ? 10 : 8;
        RootBorder.ToolTip = step.Type == MacroNodeType.RepeatStart
            ? "Repeat block start. Selects the full block."
            : "Repeat block end. Selects the full block.";

        var lineColor = IsSelected
            ? Color.FromRgb(216, 180, 254)
            : Color.FromArgb(150, 168, 85, 247);

        BoundaryLine.Background = new SolidColorBrush(lineColor);
        BoundaryLine.Height = IsSelected ? 34 : 28;
        BoundaryLine.Width = IsSelected ? 4 : 3;
    }
}
