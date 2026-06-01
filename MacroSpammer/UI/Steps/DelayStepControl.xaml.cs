using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.Services.Timeline;
using MacroSpammer.UI.Config;

namespace MacroSpammer.UI.Steps;

public partial class DelayStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;
    private bool _isEditing;

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

    public bool IsValueEditorSource(DependencyObject? source)
    {
        return FindEditorTextBox(source) != null;
    }

    public void FocusValueEditor(DependencyObject? source = null)
    {
        var textBox = FindEditorTextBox(source);
        if (textBox == null)
            return;

        textBox.Focus();
        textBox.SelectAll();
    }

    private TextBox? FindEditorTextBox(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ValueTextBox))
                return ValueTextBox;

            if (ReferenceEquals(source, MinValueTextBox))
                return MinValueTextBox;

            if (ReferenceEquals(source, MaxValueTextBox))
                return MaxValueTextBox;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        var ui = GeneratedUiConfig.DelayStep;

        if (step.Type == MacroStepType.RandomDelay)
        {
            RootBorder.Width = 84;
            FixedDelayPanel.Visibility = Visibility.Collapsed;
            RandomDelayPanel.Visibility = Visibility.Visible;
            UnitTextBlock.Visibility = Visibility.Collapsed;
            RandomUnitPanel.Visibility = Visibility.Visible;

            var minMs = Math.Min(step.RandomDelayMinMs, step.RandomDelayMaxMs);
            var maxMs = Math.Max(step.RandomDelayMinMs, step.RandomDelayMaxMs);
            var (minValue, minUnit) = DelayFormatter.Split(minMs);
            var (maxValue, maxUnit) = DelayFormatter.Split(maxMs);

            MinValueTextBox.Text = minValue;
            MaxValueTextBox.Text = maxValue;
            MinUnitTextBlock.Text = minUnit;
            MaxUnitTextBlock.Text = maxUnit;
        }
        else
        {
            RootBorder.Width = 54;
            FixedDelayPanel.Visibility = Visibility.Visible;
            RandomDelayPanel.Visibility = Visibility.Collapsed;
            UnitTextBlock.Visibility = Visibility.Visible;
            RandomUnitPanel.Visibility = Visibility.Collapsed;

            var (value, unit) = DelayFormatter.Split(step.DelayMs);
            ValueTextBox.Text = value;
            UnitTextBlock.Text = unit;
        }

        var bg = IsSelected ? ui.BackgroundSelected : ui.Background;
        var border = IsSelected ? ui.BorderSelected : ui.Border;
        var valueColor = IsSelected ? ui.ValueTextSelected : ui.ValueText;
        var unitColor = IsSelected ? ui.UnitTextSelected : ui.UnitText;

        RootBorder.Background = UiBrushes.Get(bg);
        RootBorder.BorderBrush = UiBrushes.Get(border);
        ValueTextBox.Foreground = UiBrushes.Get(valueColor);
        MinValueTextBox.Foreground = UiBrushes.Get(valueColor);
        MaxValueTextBox.Foreground = UiBrushes.Get(valueColor);
        UnitTextBlock.Foreground = UiBrushes.Get(unitColor);
        MinUnitTextBlock.Foreground = UiBrushes.Get(unitColor);
        MaxUnitTextBlock.Foreground = UiBrushes.Get(unitColor);
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

        _isEditing = true;

        if (Step.Type == MacroStepType.RandomDelay)
        {
            MinValueTextBox.Text = Step.RandomDelayMinMs.ToString();
            MaxValueTextBox.Text = Step.RandomDelayMaxMs.ToString();
            MinUnitTextBlock.Text = "ms";
            MaxUnitTextBlock.Text = "ms";
        }
        else
        {
            ValueTextBox.Text = Step.DelayMs.ToString();
        }

        if (sender is TextBox textBox)
            textBox.SelectAll();
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

        if (!_isEditing)
            return;

        _isEditing = false;

        if (Step.Type == MacroStepType.RandomDelay)
        {
            if (int.TryParse(MinValueTextBox.Text, out var min))
                Step.RandomDelayMinMs = Math.Max(0, min);

            if (int.TryParse(MaxValueTextBox.Text, out var max))
                Step.RandomDelayMaxMs = Math.Max(0, max);

            if (Step.RandomDelayMaxMs < Step.RandomDelayMinMs)
                (Step.RandomDelayMinMs, Step.RandomDelayMaxMs) = (Step.RandomDelayMaxMs, Step.RandomDelayMinMs);
        }
        else if (int.TryParse(ValueTextBox.Text, out var value))
        {
            Step.DelayMs = Math.Max(0, value);
        }

        UpdateVisual();
        DelayCommitted?.Invoke(this, EventArgs.Empty);
    }
}
