using System.Windows.Controls;
using System.Windows.Input;
using MacroSpammer.UI.Common.EntryBlocks;

namespace MacroSpammer.UI.Inspector;

public partial class InspectorWindow
{
    private void WireNumberTextBox(TextBox textBox, Func<int> currentValue, Action<int> commit)
    {
        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) => textBox.SelectAll();
        textBox.LostFocus += (_, _) => CommitNumberText(textBox, currentValue(), commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitNumberText(textBox, currentValue(), commit);
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private void WireDelayTextBox(TimeEntryBlock entry, Func<int> currentValue, Action<int> commit)
    {
        var textBox = entry.TextBox;

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.Text = currentValue().ToString();
            entry.UnitText.Text = "ms";
            textBox.SelectAll();
        };
        textBox.LostFocus += (_, _) => CommitDelayText(entry, currentValue(), commit);
        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitDelayText(entry, currentValue(), commit);
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private void CommitNumberText(TextBox textBox, int originalValue, Action<int> commit)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return;

        if (!int.TryParse(textBox.Text, out var value))
            value = 0;

        value = Math.Max(0, value);
        textBox.Text = value.ToString();

        if (value != originalValue)
            commit(value);
    }

    private void CommitDelayText(TimeEntryBlock entry, int originalValue, Action<int> commit)
    {
        if (_isSettingTimelineState || !_isEditingEnabled)
            return;

        var textBox = entry.TextBox;
        if (!int.TryParse(textBox.Text, out var value))
            value = 0;

        value = Math.Max(0, value);
        if (value != originalValue)
        {
            commit(value);
            return;
        }

        entry.SetDisplay(value);
    }
}