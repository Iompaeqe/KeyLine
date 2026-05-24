using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;
using KeySpammer.UI.Config;

namespace KeySpammer.UI.Controls;

public partial class DelayStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;

    public event EventHandler? DelayCommitted;

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

    public DelayStepControl()
    {
        InitializeComponent();
    }

    private void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        var ui = GeneratedUiConfig.DelayStep;
        var (value, unit) = DelayFormatter.Split(step.DelayMs);

        ValueTextBox.Text = value;
        UnitTextBlock.Text = unit;

        var bg = IsSelected ? ui.BackgroundSelected : ui.Background;
        var border = IsSelected ? ui.BorderSelected : ui.Border;
        var valueColor = IsSelected ? ui.ValueTextSelected : ui.ValueText;
        var unitColor = IsSelected ? ui.UnitTextSelected : ui.UnitText;

        RootBorder.Background = UiBrushes.Get(bg);
        RootBorder.BorderBrush = UiBrushes.Get(border);
        ValueTextBox.Foreground = UiBrushes.Get(valueColor);
        UnitTextBlock.Foreground = UiBrushes.Get(unitColor);
        Divider.Background = UiBrushes.Get(border);
        Divider.Opacity = IsSelected ? ui.DividerOpacitySelected : ui.DividerOpacity;
    }

    private void ValueTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void ValueTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Step == null)
            return;

        ValueTextBox.Text = Step.DelayMs.ToString();
        ValueTextBox.SelectAll();
    }

    private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitDelay();
    }

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CommitDelay();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitDelay()
    {
        if (Step == null)
            return;

        if (int.TryParse(ValueTextBox.Text, out var value))
            Step.DelayMs = Math.Max(0, value);

        UpdateVisual();
        DelayCommitted?.Invoke(this, EventArgs.Empty);
    }
}
