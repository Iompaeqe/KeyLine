using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeySpammer.Domain;
using KeySpammer.Services.Timeline;

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

        var (value, unit) = DelayFormatter.Split(step.DelayMs);
        ValueTextBox.Text = value;
        UnitTextBlock.Text = unit;

        var bg = IsSelected
            ? Color.FromRgb(30, 41, 59)
            : Color.FromRgb(20, 28, 40);

        var border = IsSelected
            ? Color.FromRgb(248, 250, 252)
            : Color.FromRgb(71, 85, 105);

        var valueColor = IsSelected
            ? Color.FromRgb(255, 251, 235)
            : Color.FromRgb(125, 211, 252);

        var unitColor = IsSelected
            ? Color.FromRgb(253, 230, 138)
            : Color.FromRgb(148, 163, 184);

        RootBorder.Background = new SolidColorBrush(bg);
        RootBorder.BorderBrush = new SolidColorBrush(border);
        ValueTextBox.Foreground = new SolidColorBrush(valueColor);
        UnitTextBlock.Foreground = new SolidColorBrush(unitColor);
        Divider.Background = new SolidColorBrush(border);
        Divider.Opacity = IsSelected ? 0.9 : 0.65;
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
