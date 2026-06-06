using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Services.Timeline;
using KeyLine.UI.Config;

namespace KeyLine.UI.Nodes;

public partial class DelayNode : NodeBase
{
    private bool _isEditing;
    private bool _isSettingText;

    public event EventHandler? DelayCommitted;

    public DelayNode()
    {
        InitializeComponent();
    }

    public override InlineEditorActivationMode GetInlineEditorActivationMode(DependencyObject? source)
    {
        return FindEditorTextBox(source) != null
            ? InlineEditorActivationMode.SuppressMouseUp
            : InlineEditorActivationMode.None;
    }

    public override void FocusInlineEditor(DependencyObject? source = null)
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

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        var ui = GeneratedUiConfig.DelayStep;

        var showRandomDelay = ShouldShowRandomDelayPanel(step);

        if (showRandomDelay)
        {
            RootBorder.Width = 84;
            FixedDelayPanel.Visibility = Visibility.Collapsed;
            RandomDelayPanel.Visibility = Visibility.Visible;
            UnitTextBlock.Visibility = Visibility.Collapsed;
            RandomUnitPanel.Visibility = Visibility.Visible;
        }
        else
        {
            RootBorder.Width = 54;
            FixedDelayPanel.Visibility = Visibility.Visible;
            RandomDelayPanel.Visibility = Visibility.Collapsed;
            UnitTextBlock.Visibility = Visibility.Visible;
            RandomUnitPanel.Visibility = Visibility.Collapsed;
        }

        if (!_isEditing)
            SetDisplayFromNode();

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
        if (Node == null)
            return;

        _isEditing = true;
        if (NormalizeDelayValues())
            DelayCommitted?.Invoke(this, EventArgs.Empty);

        _isSettingText = true;
        try
        {
            if (IsRandomDelayVisual())
            {
                var (min, max) = GetDisplayDelayRange(Node);
                MinValueTextBox.Text = min.ToString();
                MaxValueTextBox.Text = max.ToString();
                MinUnitTextBlock.Text = "ms";
                MaxUnitTextBlock.Text = "ms";
            }
            else
            {
                var (min, _) = GetDisplayDelayRange(Node);
                ValueTextBox.Text = min.ToString();
                UnitTextBlock.Text = "ms";
            }
        }
        finally
        {
            _isSettingText = false;
        }

        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!IsKeyboardFocusWithin)
                    EndDelayEdit();
            }));
    }

    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CommitDelayTextChange();
    }

    private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        EndDelayEdit();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitDelayTextChange()
    {
        if (Node == null)
            return;

        if (!_isEditing || _isSettingText)
            return;

        var changed = false;

        if (IsRandomDelayVisual())
        {
            var min = ParseDelayValue(MinValueTextBox.Text);
            var max = ParseDelayValue(MaxValueTextBox.Text);

            if (Node.Type != MacroNodeType.Delay)
            {
                Node.Type = MacroNodeType.Delay;
                changed = true;
            }

            if (Node.MinDelayMs != min)
            {
                Node.MinDelayMs = min;
                changed = true;
            }

            if (Node.MaxDelayMs != max)
            {
                Node.MaxDelayMs = max;
                changed = true;
            }

            if (Node.DelayMs != min)
            {
                Node.DelayMs = min;
                changed = true;
            }

            if (Node.RandomDelayMinMs != min)
            {
                Node.RandomDelayMinMs = min;
                changed = true;
            }

            if (Node.RandomDelayMaxMs != max)
            {
                Node.RandomDelayMaxMs = max;
                changed = true;
            }
        }
        else
        {
            var value = ParseDelayValue(ValueTextBox.Text);
            var (min, max) = Node.GetEffectiveDelayRange();
            if (Node.Type != MacroNodeType.Delay || min != value || max != value)
            {
                Node.SetDelayRange(value, value);
                changed = true;
            }
        }

        if (changed)
            DelayCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void EndDelayEdit()
    {
        if (Node == null || !_isEditing)
            return;

        _isEditing = false;

        var changed = NormalizeDelayValues();

        UpdateVisual();

        if (changed)
            DelayCommitted?.Invoke(this, EventArgs.Empty);
    }

    private void SetDisplayFromNode()
    {
        if (Node == null)
            return;

        _isSettingText = true;
        try
        {
            if (ShouldShowRandomDelayPanel(Node))
            {
                var (minMs, maxMs) = GetDisplayDelayRange(Node);
                var (minValue, minUnit) = DelayFormatter.Split(minMs);
                var (maxValue, maxUnit) = DelayFormatter.Split(maxMs);

                MinValueTextBox.Text = minValue;
                MaxValueTextBox.Text = maxValue;
                MinUnitTextBlock.Text = minUnit;
                MaxUnitTextBlock.Text = maxUnit;
            }
            else
            {
                var (minMs, _) = GetDisplayDelayRange(Node);
                var (value, unit) = DelayFormatter.Split(minMs);
                ValueTextBox.Text = value;
                UnitTextBlock.Text = unit;
            }
        }
        finally
        {
            _isSettingText = false;
        }
    }

    private static int ParseDelayValue(string text) =>
        long.TryParse(text, out var value)
            ? DelayFormatter.ClampMilliseconds(value)
            : string.IsNullOrWhiteSpace(text) ? 0 : DelayFormatter.MaxMilliseconds;

    private bool NormalizeDelayValues()
    {
        if (Node == null)
            return false;

        var (min, max) = GetDisplayDelayRange(Node);
        if (Node.Type == MacroNodeType.Delay &&
            Node.MinDelayMs == min &&
            Node.MaxDelayMs == max &&
            Node.DelayMs == min &&
            Node.RandomDelayMinMs == min &&
            Node.RandomDelayMaxMs == max)
        {
            return false;
        }

        Node.SetDelayRange(min, max);
        return true;
    }

    private bool IsRandomDelayVisual() =>
        RandomDelayPanel.Visibility == Visibility.Visible;

    private bool ShouldShowRandomDelayPanel(MacroNode node) =>
        node.HasRandomDelayRange() || (_isEditing && IsRandomDelayVisual());

    private static (int MinMs, int MaxMs) GetDisplayDelayRange(MacroNode node)
    {
        var (min, max) = node.GetEffectiveDelayRange();
        min = DelayFormatter.ClampMilliseconds(min);
        max = DelayFormatter.ClampMilliseconds(max);

        if (max < min)
            (min, max) = (max, min);

        return (min, max);
    }
}
