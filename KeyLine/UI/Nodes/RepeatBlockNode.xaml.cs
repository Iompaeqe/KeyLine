using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public partial class RepeatBlockNode : BlockNodeBase
{
    private bool _isEditing;

    public event EventHandler? RepeatCommitted;

    public RepeatBlockNode()
    {
        InitializeComponent();
    }

    public override InlineEditorActivationMode GetInlineEditorActivationMode(DependencyObject? source)
    {
        return IsBlockStart && FindCountTextBox(source) != null
            ? InlineEditorActivationMode.SuppressMouseUp
            : InlineEditorActivationMode.None;
    }

    public override void FocusInlineEditor(DependencyObject? source = null)
    {
        if (!IsBlockStart)
            return;

        CountTextBox.Focus();
        CountTextBox.SelectAll();
    }

    protected override void UpdateVisual()
    {
        var step = Node;
        if (step == null)
            return;

        var isStart = step.Type == MacroNodeType.RepeatStart;
        RootBorder.Width = isStart ? 76 : 68;
        RootBorder.ToolTip = isStart
            ? "Repeat every node until the matching Repeat End."
            : "End of this repeat block.";

        TitleTextBlock.Text = "REPEAT";
        DetailTextBlock.Text = isStart ? "times" : "block";

        CountTextBox.Visibility = isStart ? Visibility.Visible : Visibility.Collapsed;
        CountTextBox.IsHitTestVisible = isStart;
        EndTextBlock.Visibility = isStart ? Visibility.Collapsed : Visibility.Visible;

        if (!_isEditing)
            CountTextBox.Text = Math.Max(0, step.RepeatCount).ToString();

        var background = isStart
            ? Color.FromRgb(58, 38, 10)
            : Color.FromRgb(31, 41, 55);
        var border = isStart
            ? Color.FromRgb(217, 119, 6)
            : Color.FromRgb(148, 163, 184);
        var foreground = isStart
            ? Color.FromRgb(254, 243, 199)
            : Color.FromRgb(226, 232, 240);
        var detail = isStart
            ? Color.FromRgb(253, 230, 138)
            : Color.FromRgb(148, 163, 184);

        if (IsSelected)
        {
            border = Color.FromRgb(250, 204, 21);
            foreground = Color.FromRgb(255, 251, 235);
        }

        RootBorder.Background = new SolidColorBrush(background);
        RootBorder.BorderBrush = new SolidColorBrush(border);
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        TitleTextBlock.Foreground = new SolidColorBrush(detail);
        CountTextBox.Foreground = new SolidColorBrush(foreground);
        EndTextBlock.Foreground = new SolidColorBrush(foreground);
        DetailTextBlock.Foreground = new SolidColorBrush(detail);
    }

    private TextBox? FindCountTextBox(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, CountTextBox))
                return CountTextBox;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void CountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void CountTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Node == null || Node.Type != MacroNodeType.RepeatStart)
            return;

        _isEditing = true;
        CountTextBox.Text = Math.Max(0, Node.RepeatCount).ToString();
        CountTextBox.SelectAll();
    }

    private void CountTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitRepeatCount();
    }

    private void CountTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CommitRepeatCount();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitRepeatCount()
    {
        if (Node == null || Node.Type != MacroNodeType.RepeatStart || !_isEditing)
            return;

        _isEditing = false;
        if (int.TryParse(CountTextBox.Text, out var repeatCount))
            Node.RepeatCount = Math.Max(1, repeatCount);

        UpdateVisual();
        RepeatCommitted?.Invoke(this, EventArgs.Empty);
    }
}
