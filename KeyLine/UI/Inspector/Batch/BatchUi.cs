using System.Windows.Controls;

namespace KeyLine.UI.Inspector.Batch;

/// <summary>
/// One consistent "values differ" indicator used by every batch-editable control: a plain "Mixed"
/// label for text fields, pagers and dropdowns, and the indeterminate (dot) state for checkboxes.
/// </summary>
public static class BatchUi
{
    public const string IndicatorText = "Mixed";

    /// <summary>Shows the mixed indicator in a text box without treating it as a real value.</summary>
    public static void ShowMixed(TextBox textBox)
    {
        ClearMixedStyle(textBox);
        textBox.Text = IndicatorText;
        textBox.ToolTip = "Multiple values — type to apply one to all selected.";
    }

    /// <summary>Reverts any styling a previous build may have applied to a text box.</summary>
    public static void ClearMixedStyle(TextBox textBox)
    {
        textBox.ClearValue(TextBox.ForegroundProperty);
        textBox.ClearValue(TextBox.FontStyleProperty);
    }
}
