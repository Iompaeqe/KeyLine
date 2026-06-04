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

        if (step.Type == MacroNodeType.RandomDelay)
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
            if (Node.Type == MacroNodeType.RandomDelay)
            {
                MinValueTextBox.Text = DelayFormatter.ClampMilliseconds(Node.RandomDelayMinMs).ToString();
                MaxValueTextBox.Text = DelayFormatter.ClampMilliseconds(Node.RandomDelayMaxMs).ToString();
                MinUnitTextBlock.Text = "ms";
                MaxUnitTextBlock.Text = "ms";
            }
            else
            {
                ValueTextBox.Text = DelayFormatter.ClampMilliseconds(Node.DelayMs).ToString();
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

        if (Node.Type == MacroNodeType.RandomDelay)
        {
            var min = ParseDelayValue(MinValueTextBox.Text);
            var max = ParseDelayValue(MaxValueTextBox.Text);

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
            if (Node.DelayMs != value)
            {
                Node.DelayMs = value;
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

        SetDisplayFromNode();

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
            if (Node.Type == MacroNodeType.RandomDelay)
            {
                var minMs = DelayFormatter.ClampMilliseconds(Math.Min(Node.RandomDelayMinMs, Node.RandomDelayMaxMs));
                var maxMs = DelayFormatter.ClampMilliseconds(Math.Max(Node.RandomDelayMinMs, Node.RandomDelayMaxMs));
                var (minValue, minUnit) = DelayFormatter.Split(minMs);
                var (maxValue, maxUnit) = DelayFormatter.Split(maxMs);

                MinValueTextBox.Text = minValue;
                MaxValueTextBox.Text = maxValue;
                MinUnitTextBlock.Text = minUnit;
                MaxUnitTextBlock.Text = maxUnit;
            }
            else
            {
                var (value, unit) = DelayFormatter.Split(DelayFormatter.ClampMilliseconds(Node.DelayMs));
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

        if (Node.Type == MacroNodeType.RandomDelay)
        {
            var min = DelayFormatter.ClampMilliseconds(Node.RandomDelayMinMs);
            var max = DelayFormatter.ClampMilliseconds(Node.RandomDelayMaxMs);
            if (max < min)
                (min, max) = (max, min);

            if (Node.RandomDelayMinMs == min && Node.RandomDelayMaxMs == max)
                return false;

            Node.RandomDelayMinMs = min;
            Node.RandomDelayMaxMs = max;
            return true;
        }

        var delay = DelayFormatter.ClampMilliseconds(Node.DelayMs);
        if (Node.DelayMs == delay)
            return false;

        Node.DelayMs = delay;
        return true;
    }
}
