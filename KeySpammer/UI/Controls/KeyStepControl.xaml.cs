using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.UI.Config;
using KeySpammer.UI.Timeline;

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

        var ui = GeneratedUiConfig.KeyStep;
        var keyText = StepDisplayFormatter.GetKeyText(step);
        var isComboKey = StepDisplayFormatter.IsComboKey(keyText);

        KeyTextBlock.Text = keyText;
        KeyTextBlock.FontSize = isComboKey ? ui.ComboFontSize : ui.NormalFontSize;

        KeyBorder.MinWidth = isComboKey ? ui.ComboMinWidth : ui.NormalMinWidth;
        KeyBorder.Padding = isComboKey ? ui.ComboPadding : ui.NormalPadding;

        if (ShowKeyUpDown)
        {
            Margin = isComboKey ? ui.ComboArrowMargin : ui.NormalArrowMargin;

            UpArrow.Visibility = step.Type == MacroStepType.KeyUp ? Visibility.Visible : Visibility.Collapsed;
            DownArrow.Visibility = step.Type == MacroStepType.KeyDown ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            Margin = ui.NoArrowMargin;
            UpArrow.Visibility = Visibility.Collapsed;
            DownArrow.Visibility = Visibility.Collapsed;
        }

        var (bg, border, fg) = step.Type switch
        {
            MacroStepType.KeyDown => (ui.KeyDownBackground, ui.KeyDownBorder, ui.KeyDownText),
            MacroStepType.KeyUp => (ui.KeyUpBackground, ui.KeyUpBorder, ui.KeyUpText),
            _ => (ui.FallbackBackground, ui.FallbackBorder, ui.FallbackText)
        };

        if (IsSelected)
        {
            border = ui.SelectedBorder;
            fg = ui.SelectedText;
        }

        KeyBorder.Background = new SolidColorBrush(bg);
        KeyBorder.BorderBrush = new SolidColorBrush(border);
        KeyBorder.BorderThickness = IsSelected
            ? ui.SelectedBorderThickness
            : ui.NormalBorderThickness;
        KeyTextBlock.Foreground = new SolidColorBrush(fg);
        UpArrow.Foreground = new SolidColorBrush(fg);
        DownArrow.Foreground = new SolidColorBrush(fg);
    }
}
