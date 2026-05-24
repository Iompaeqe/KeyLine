using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.UI.Config;
using KeySpammer.UI.Timeline;

namespace KeySpammer.UI.Controls;

public partial class TextStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;

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

    public TextStepControl()
    {
        InitializeComponent();
    }

    private void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        var ui = GeneratedUiConfig.TextStep;
        var preview = StepDisplayFormatter.GetTextPreview(step);

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