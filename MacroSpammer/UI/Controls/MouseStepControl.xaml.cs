using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;

namespace MacroSpammer.UI.Controls;

public partial class MouseStepControl : UserControl
{
    private MacroStep? _step;
    private bool _isSelected;
    private bool _isUpdating;

    public event EventHandler? CoordinateCommitted;
    public event EventHandler? TargetPickRequested;

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

    public MouseStepControl()
    {
        InitializeComponent();
    }

    public bool IsEditorSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, XTextBox) ||
                ReferenceEquals(source, YTextBox) ||
                ReferenceEquals(source, TargetButton))
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void UpdateVisual()
    {
        var step = Step;
        if (step == null)
            return;

        _isUpdating = true;
        XTextBox.Text = step.MouseX.ToString();
        YTextBox.Text = step.MouseY.ToString();
        _isUpdating = false;

        ActionTextBlock.Text = step.Type switch
        {
            MacroStepType.CursorMove => "MOVE",
            MacroStepType.MouseDown => "BG DOWN",
            MacroStepType.MouseUp => "BG UP",
            MacroStepType.MouseClick => "BG CLICK",
            _ => "MOUSE"
        };

        RootBorder.Background = new SolidColorBrush(IsSelected
            ? Color.FromRgb(48, 34, 84)
            : Color.FromRgb(30, 25, 46));
        RootBorder.BorderBrush = new SolidColorBrush(IsSelected
            ? Color.FromRgb(168, 85, 247)
            : Color.FromRgb(86, 66, 120));
        RootBorder.BorderThickness = IsSelected ? new Thickness(2) : new Thickness(1);
        ActionTextBlock.Foreground = new SolidColorBrush(IsSelected
            ? Color.FromRgb(243, 232, 255)
            : Color.FromRgb(216, 180, 254));
    }

    private void CoordinateTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void CoordinateTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.SelectAll();
    }

    private void CoordinateTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitCoordinates();
    }

    private void CoordinateTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CommitCoordinates();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void TargetButton_Click(object sender, RoutedEventArgs e)
    {
        TargetPickRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CommitCoordinates()
    {
        if (_isUpdating || Step == null)
            return;

        if (int.TryParse(XTextBox.Text, out var x))
            Step.MouseX = Math.Max(0, x);

        if (int.TryParse(YTextBox.Text, out var y))
            Step.MouseY = Math.Max(0, y);

        UpdateVisual();
        CoordinateCommitted?.Invoke(this, EventArgs.Empty);
    }
}
