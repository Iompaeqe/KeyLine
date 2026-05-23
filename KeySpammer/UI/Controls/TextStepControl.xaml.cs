using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeySpammer.Domain;

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

        var preview = GetTextPreview(step.Text);
        RootBorder.Width = Math.Max(110, Math.Min(220, (preview.Length * 8) + 34));
        RootBorder.ToolTip = step.Text;
        PreviewTextBlock.Text = preview;

        var bg = Color.FromRgb(27, 45, 74);
        var border = Color.FromRgb(96, 165, 250);
        var fg = Color.FromRgb(226, 238, 255);

        if (IsSelected)
        {
            border = Color.FromRgb(248, 250, 252);
            fg = Color.FromRgb(255, 255, 255);
        }

        RootBorder.Background = new SolidColorBrush(bg);
        RootBorder.BorderBrush = new SolidColorBrush(border);
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        PreviewTextBlock.Foreground = new SolidColorBrush(fg);
    }

    private static string GetTextPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "TXT";

        text = text.Replace("\r", " ").Replace("\n", " ");
        return text.Length <= 18 ? text : text[..18] + "…";
    }
}
