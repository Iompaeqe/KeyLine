using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer.UI.Steps;

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

        SetKeyText(keyText, UiBrushes.Get(Color.FromRgb(45, 212, 191)));
        KeyTextBlock.FontSize = isComboKey ? ui.ComboFontSize : ui.NormalFontSize;

        KeyBorder.MinWidth = isComboKey ? ui.ComboMinWidth : ui.NormalMinWidth;
        KeyBorder.Padding = isComboKey ? ui.ComboPadding : ui.NormalPadding;

        if (ShowKeyUpDown)
        {
            Margin = isComboKey ? ui.ComboArrowMargin : ui.NormalArrowMargin;

            UpArrow.Visibility = step.Type is MacroStepType.KeyUp or MacroStepType.ForegroundMouseUp ? Visibility.Visible : Visibility.Collapsed;
            DownArrow.Visibility = step.Type is MacroStepType.KeyDown or MacroStepType.ForegroundMouseDown ? Visibility.Visible : Visibility.Collapsed;
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
            MacroStepType.ForegroundMouseDown => (Color.FromRgb(15, 73, 70), Color.FromRgb(45, 212, 191), Color.FromRgb(204, 251, 241)),
            MacroStepType.ForegroundMouseUp => (Color.FromRgb(45, 55, 72), Color.FromRgb(94, 234, 212), Color.FromRgb(204, 251, 241)),
            _ => (ui.FallbackBackground, ui.FallbackBorder, ui.FallbackText)
        };

        if (IsSelected)
        {
            border = ui.SelectedBorder;
            fg = ui.SelectedText;
        }

        KeyBorder.Background = UiBrushes.Get(bg);
        KeyBorder.BorderBrush = UiBrushes.Get(border);
        KeyBorder.BorderThickness = IsSelected
            ? ui.SelectedBorderThickness
            : ui.NormalBorderThickness;
        KeyTextBlock.Foreground = UiBrushes.Get(fg);
        SetKeyText(keyText, UiBrushes.Get(Color.FromRgb(45, 212, 191)));
        UpArrow.Foreground = UiBrushes.Get(fg);
        DownArrow.Foreground = UiBrushes.Get(fg);
    }

    private void SetKeyText(string text, Brush mouseBrush)
    {
        KeyTextBlock.Inlines.Clear();

        var parts = text.Split('+');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0)
                KeyTextBlock.Inlines.Add(new Run("+"));

            var part = parts[i];
            var run = new Run(part);
            if (IsMouseToken(part))
                run.Foreground = mouseBrush;

            KeyTextBlock.Inlines.Add(run);
        }
    }

    private static bool IsMouseToken(string text)
    {
        return text.Length == 2 &&
               text[0] == 'M' &&
               text[1] is >= '1' and <= '5';
    }
}
