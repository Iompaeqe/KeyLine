using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeySpammer.Domain;

namespace KeySpammer.UI.Controls;

public partial class KeyStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;
    private bool _showKeyUpDown;

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

    public bool ShowKeyUpDown
    {
        get => _showKeyUpDown;
        set
        {
            _showKeyUpDown = value;
            UpdateVisual();
        }
    }

    public KeyStepControl()
    {
        InitializeComponent();
    }

    private void UpdateVisual()
    {
        if (!IsLoaded && KeyBorder == null)
            return;

        var step = Step;
        if (step == null)
            return;

        var keyText = step.KeyName;
        var isComboKey = keyText.Contains('+');

        KeyTextBlock.Text = keyText;
        KeyTextBlock.FontSize = isComboKey ? 16 : 22;

        KeyBorder.MinWidth = isComboKey ? 110 : 62;
        KeyBorder.Padding = isComboKey
            ? new Thickness(12, 0, 12, 0)
            : new Thickness(10, 0, 10, 0);

        if (ShowKeyUpDown)
        {
            Margin = isComboKey
                ? new Thickness(7, 0, 7, 0)
                : new Thickness(8, 0, 8, 0);

            UpArrow.Visibility = step.Type == MacroStepType.KeyUp ? Visibility.Visible : Visibility.Collapsed;
            DownArrow.Visibility = step.Type == MacroStepType.KeyDown ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            Margin = new Thickness(8, 12, 8, 12);
            UpArrow.Visibility = Visibility.Collapsed;
            DownArrow.Visibility = Visibility.Collapsed;
        }

        var (bg, border, fg) = step.Type switch
        {
            MacroStepType.KeyDown =>
                (Color.FromRgb(20, 52, 96), Color.FromRgb(80, 150, 255), Color.FromRgb(230, 243, 255)),

            MacroStepType.KeyUp =>
                (Color.FromRgb(35, 45, 98), Color.FromRgb(125, 115, 255), Color.FromRgb(238, 236, 255)),

            _ =>
                (Color.FromRgb(30, 41, 59), Color.FromRgb(100, 116, 139), Color.FromRgb(226, 232, 240))
        };

        if (IsSelected)
        {
            border = Color.FromRgb(248, 250, 252);
            fg = Color.FromRgb(255, 255, 255);
        }

        KeyBorder.Background = new SolidColorBrush(bg);
        KeyBorder.BorderBrush = new SolidColorBrush(border);
        KeyBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        KeyTextBlock.Foreground = new SolidColorBrush(fg);
        UpArrow.Foreground = new SolidColorBrush(fg);
        DownArrow.Foreground = new SolidColorBrush(fg);
    }
}
