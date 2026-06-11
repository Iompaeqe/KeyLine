using System.Windows.Controls;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Fields;

/// <summary>
/// The single entry point for wiring inspector input controls. Every text/number/delay/combo/checkbox
/// field — single or batch, timeline or node — is created here so focus, commit timing, mixed-value
/// handling and Enter/Escape/LostFocus behavior are defined exactly once. Single edits are modelled as a
/// "batch of one" via <see cref="BatchValue{T}"/>, so single and batch share one code path.
/// </summary>
public static class InspectorFieldBinder
{
    public static IInspectorField BindNumber(
        InspectorFieldHost host,
        NumberEntryBlock entry,
        Func<BatchValue<int>> read,
        Action<int> apply,
        int min,
        int? max,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = host.CanEdit && isEnabled;
        entry.IsEnabled = canEdit;
        entry.ToolTip = tooltip;

        var textBox = entry.TextBox;
        textBox.IsEnabled = canEdit;

        var field = new NumericInspectorField(
            textBox,
            host,
            read,
            apply,
            decide: (text, batch) => InspectorNumberCommit.Decide(text, batch, min, max),
            renderDisplay: value =>
            {
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = value.ToString();
            },
            renderEdit: value => textBox.Text = value.ToString(),
            renderMixedDisplay: () => BatchUi.ShowMixed(textBox),
            renderEditEmpty: () =>
            {
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = string.Empty;
            });

        host.Register(field);
        return field;
    }

    public static IInspectorField BindDelay(
        InspectorFieldHost host,
        TimeEntryBlock entry,
        Func<BatchValue<int>> read,
        Action<int> apply,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = host.CanEdit && isEnabled;
        entry.IsEnabled = canEdit;
        entry.ToolTip = tooltip;

        var textBox = entry.TextBox;
        textBox.IsEnabled = canEdit;

        var field = new NumericInspectorField(
            textBox,
            host,
            read,
            apply,
            decide: (text, batch) => InspectorNumberCommit.DecideDelay(text, batch),
            renderDisplay: value =>
            {
                BatchUi.ClearMixedStyle(textBox);
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(value));
            },
            renderEdit: value =>
            {
                textBox.Text = DelayFormatter.ClampMilliseconds(value).ToString();
                entry.UnitText.Text = "ms";
            },
            renderMixedDisplay: () =>
            {
                BatchUi.ShowMixed(textBox);
                entry.UnitText.Text = string.Empty;
            },
            renderEditEmpty: () =>
            {
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = string.Empty;
                entry.UnitText.Text = "ms";
            });

        host.Register(field);
        return field;
    }

    public static IInspectorField BindText(
        InspectorFieldHost host,
        TextBox textBox,
        Func<BatchValue<string>> read,
        Action<string> apply,
        bool isEnabled,
        bool multiline = false,
        bool selectAllOnFocus = true,
        string? tooltip = null)
    {
        textBox.IsEnabled = host.CanEdit && isEnabled;
        if (tooltip != null)
            textBox.ToolTip = tooltip;

        var field = new TextInspectorField(textBox, host, read, apply, multiline, selectAllOnFocus);
        host.Register(field);
        return field;
    }

    public static IInspectorField BindCombo<TValue>(
        InspectorFieldHost host,
        ComboBox combo,
        Func<BatchValue<TValue>> read,
        Action<TValue> apply,
        bool isEnabled,
        IEqualityComparer<TValue>? comparer = null)
    {
        combo.IsEnabled = host.CanEdit && isEnabled;
        var field = new ComboInspectorField<TValue>(combo, host, read, apply, comparer);
        host.Register(field);
        return field;
    }

    public static IInspectorField BindCheckBox(
        InspectorFieldHost host,
        CheckBox checkBox,
        Func<BatchValue<bool>> read,
        Action<bool> apply,
        bool isEnabled)
    {
        checkBox.IsEnabled = host.CanEdit && isEnabled;
        var field = new CheckBoxInspectorField(checkBox, host, read, apply);
        host.Register(field);
        return field;
    }

    /// <summary>Read helper for a single-item (non-batch) source — the "batch of one" case.</summary>
    public static Func<BatchValue<int>> SingleInt(Func<int> value) => () => BatchValue<int>.Common(value());

    /// <summary>Read helper for a single-item (non-batch) string source.</summary>
    public static Func<BatchValue<string>> SingleText(Func<string> value) =>
        () => BatchValue<string>.Common(value() ?? string.Empty);

    /// <summary>Read helper for a single-item (non-batch) value source.</summary>
    public static Func<BatchValue<TValue>> SingleValue<TValue>(Func<TValue> value) =>
        () => BatchValue<TValue>.Common(value());
}
