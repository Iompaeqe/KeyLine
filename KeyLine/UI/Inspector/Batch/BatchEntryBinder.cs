using System.Linq;
using System.Windows.Input;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Batch;

/// <summary>
/// Shared wiring for batch-editing a numeric field across a set of nodes. Shows a "Mixed" indicator
/// when the selected nodes disagree, clears it on focus, and only writes when the user actually enters
/// a value — never normalizing a mixed field just because the inspector opened or another field changed.
/// </summary>
public static class BatchEntryBinder
{
    public static void BindNumber(
        NumberEntryBlock entry,
        NodeInspectorContext context,
        Func<BatchValue<int>> read,
        Action<int> apply,
        int min,
        int max,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.IsEnabled = canEdit;
        entry.ToolTip = $"{tooltip} Applies to all selected nodes.";

        var textBox = entry.TextBox;
        textBox.IsEnabled = canEdit;

        var isSettingText = false;

        void ApplyDisplay()
        {
            var batch = read();
            isSettingText = true;
            try
            {
                if (batch.HasMixedValue)
                {
                    BatchUi.ShowMixed(textBox);
                }
                else
                {
                    BatchUi.ClearMixedStyle(textBox);
                    textBox.Text = batch.Value.ToString();
                }
            }
            finally
            {
                isSettingText = false;
            }
        }

        // Returns true when a value was actually applied to the selected nodes.
        bool Commit()
        {
            if (isSettingText || context.IsRefreshing())
                return false;

            var batch = read();

            if (!int.TryParse(textBox.Text, out var value))
            {
                // Empty / non-numeric while mixed means "leave it mixed".
                if (batch.HasMixedValue)
                    return false;

                value = min;
            }

            value = Math.Clamp(value, min, max);
            if (!batch.HasMixedValue && batch.Value == value)
                return false;

            apply(value);
            return true;
        }

        ApplyDisplay();

        textBox.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);

        textBox.GotKeyboardFocus += (_, _) =>
        {
            var batch = read();
            isSettingText = true;
            try
            {
                // Drop the mixed styling on focus so the user types a normal value into an empty field.
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = batch.HasMixedValue ? string.Empty : batch.Value.ToString();
            }
            finally
            {
                isSettingText = false;
            }

            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            if (!Commit())
                ApplyDisplay();
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            if (!Commit())
                ApplyDisplay();

            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }
}
