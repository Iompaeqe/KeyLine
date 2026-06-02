using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer.UI.Nodes;

public partial class TextNode : NodeBase
{
    public TextNode()
    {
        InitializeComponent();
    }

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        var ui = GeneratedUiConfig.TextStep;
        var preview = NodeDisplayFormatter.GetTextPreview(step);

        RootBorder.Width = Math.Max(
            ui.MinWidth,
            Math.Min(ui.MaxWidth, (preview.Length * ui.WidthPerCharacter) + ui.WidthPadding));

        RootBorder.ToolTip = step.Text;
        PreviewTextBlock.Text = preview;

        var bg = ui.Background;
        var border = IsSelected ? ui.BorderSelected : ui.Border;
        var fg = IsSelected ? ui.TextSelected : ui.Text;

        RootBorder.Background = UiBrushes.Get(bg);
        RootBorder.BorderBrush = UiBrushes.Get(border);
        RootBorder.BorderThickness = IsSelected
            ? ui.SelectedBorderThickness
            : ui.NormalBorderThickness;
        PreviewTextBlock.Foreground = UiBrushes.Get(fg);
    }
}
