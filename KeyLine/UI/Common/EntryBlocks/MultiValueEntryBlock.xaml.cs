using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeyLine.UI.Common.EntryBlocks;

public sealed record MultiValueEntryField(
    string Label,
    int Value,
    Action<int> Commit,
    int Min = 0,
    int? Max = null);

public partial class MultiValueEntryBlock : UserControl
{
    public MultiValueEntryBlock()
    {
        InitializeComponent();
    }

    public IReadOnlyList<TextBox> TextBoxes { get; private set; } = [];

    public void SetFields(
        IEnumerable<MultiValueEntryField> fields,
        Func<bool> isRefreshing,
        bool isEditingEnabled,
        string? tooltip = null)
    {
        FieldsPanel.Children.Clear();
        var textBoxes = new List<TextBox>();

        foreach (var field in fields)
        {
            var host = CreateFieldHost(field.Label, tooltip);
            var textBox = CreateTextBox(field, isEditingEnabled, tooltip);

            textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
            textBox.GotKeyboardFocus += (_, _) => textBox.SelectAll();
            textBox.LostFocus += (_, _) =>
                CommitNumberText(textBox, field, isRefreshing());
            textBox.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter)
                    return;

                CommitNumberText(textBox, field, isRefreshing());
                Keyboard.ClearFocus();
                e.Handled = true;
            };

            host.Children.Add(textBox);
            FieldsPanel.Children.Add(host);
            textBoxes.Add(textBox);
        }

        TextBoxes = textBoxes;
    }

    private static StackPanel CreateFieldHost(string label, string? tooltip)
    {
        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
            ToolTip = tooltip
        };

        host.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 0),
            ToolTip = tooltip
        });

        return host;
    }

    private static TextBox CreateTextBox(MultiValueEntryField field, bool isEditingEnabled, string? tooltip) =>
        new()
        {
            Text = field.Value.ToString(),
            Width = 30,
            Height = 18,
            Padding = new Thickness(0, 1, 2, 1),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            CaretBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            SelectionBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsEnabled = isEditingEnabled,
            ToolTip = tooltip
        };

    private static void CommitNumberText(TextBox textBox, MultiValueEntryField field, bool isRefreshing)
    {
        if (isRefreshing)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            value = field.Min;

        value = Math.Max(field.Min, value);
        if (field.Max.HasValue)
            value = Math.Min(field.Max.Value, value);

        textBox.Text = value.ToString();

        if (value != field.Value)
            field.Commit(value);
    }
}
