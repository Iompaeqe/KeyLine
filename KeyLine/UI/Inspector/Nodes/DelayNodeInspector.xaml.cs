using System.Collections.Generic;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;

namespace KeyLine.UI.Inspector.Nodes;

public partial class DelayNodeInspector
{
    private TimeEntryBlock MinDelayEntry => MinDelayRow.GetContent<TimeEntryBlock>()!;

    private TimeEntryBlock MaxDelayEntry => MaxDelayRow.GetContent<TimeEntryBlock>()!;

    public DelayNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        BindDelayEntry(
            entry: MinDelayEntry,
            context: context,
            currentValue: () => GetDelayMinimum(node),
            commit: value => context.CommitNodeChange(() => SetDelayMinimum(node, value)),
            tooltip: TooltipNotes.DelayNodeMinimum,
            isEnabled: policy.CanEditDelay,
            commitOnTextChanged: false);

        BindDelayEntry(
            entry: MaxDelayEntry,
            context: context,
            currentValue: () => GetDelayMaximum(node),
            commit: value => context.CommitNodeChange(() => SetDelayMaximum(node, value)),
            tooltip: TooltipNotes.DelayNodeMaximum,
            isEnabled: policy.CanEditDelay,
            commitOnTextChanged: false);
    }

    /// <summary>
    /// Batch mode: edits Min/Max delay across every selected delay node at once. Mixed values show a
    /// "Mixed" indicator and are left untouched unless the user actually types a value. Each commit
    /// writes all nodes inside a single undo step (via CommitNodeChange).
    /// </summary>
    public void BindBatch(NodeInspectorContext context, IReadOnlyList<MacroNode> nodes)
    {
        BindBatchDelayEntry(
            entry: MinDelayEntry,
            context: context,
            read: () => BatchValues.Read(nodes, GetDelayMinimum),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    SetDelayMinimum(node, value);
            }),
            tooltip: TooltipNotes.DelayNodeMinimum);

        BindBatchDelayEntry(
            entry: MaxDelayEntry,
            context: context,
            read: () => BatchValues.Read(nodes, GetDelayMaximum),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    SetDelayMaximum(node, value);
            }),
            tooltip: TooltipNotes.DelayNodeMaximum);
    }

    private static void BindBatchDelayEntry(
        TimeEntryBlock entry,
        NodeInspectorContext context,
        Func<BatchValue<int>> read,
        Action<int> apply,
        string tooltip)
    {
        var canEdit = context.CanEditOption(true);

        entry.ToolTip = $"{tooltip} Click to edit in milliseconds. Applies to all selected nodes.";
        entry.IsEnabled = canEdit;
        entry.TextBox.IsEnabled = canEdit;

        var textBox = entry.TextBox;
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
                    entry.UnitText.Text = string.Empty;
                }
                else
                {
                    BatchUi.ClearMixedStyle(textBox);
                    entry.SetDisplay(DelayFormatter.ClampMilliseconds(batch.Value));
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
            var text = textBox.Text;

            if (!long.TryParse(text, out var raw))
            {
                // Empty or non-numeric while mixed means "leave it mixed" — never normalize on commit.
                if (batch.HasMixedValue)
                    return false;

                raw = string.IsNullOrWhiteSpace(text) ? 0 : DelayFormatter.MaxMilliseconds;
            }

            var clamped = DelayFormatter.ClampMilliseconds(raw);
            if (!batch.HasMixedValue && batch.Value == clamped)
                return false;

            apply(clamped);
            return true;
        }

        ApplyDisplay();

        textBox.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };

        textBox.GotKeyboardFocus += (_, _) =>
        {
            var batch = read();
            isSettingText = true;
            try
            {
                BatchUi.ClearMixedStyle(textBox);
                textBox.Text = batch.HasMixedValue
                    ? string.Empty
                    : DelayFormatter.ClampMilliseconds(batch.Value).ToString();
                entry.UnitText.Text = "ms";
            }
            finally
            {
                isSettingText = false;
            }

            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            // If a value was applied, CommitNodeChange rebuilds the inspector, replacing this control.
            // Otherwise restore the display (e.g. a mixed field the user focused but left unchanged).
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

    private static void BindDelayEntry(
        TimeEntryBlock entry,
        NodeInspectorContext context,
        Func<int> currentValue,
        Action<int> commit,
        string tooltip,
        bool isEnabled,
        bool commitOnTextChanged)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.ToolTip = $"{tooltip} Click to edit in milliseconds.";
        entry.IsEnabled = canEdit;
        entry.TextBox.IsEnabled = canEdit;
        entry.SetDisplay(DelayFormatter.ClampMilliseconds(currentValue()));

        var textBox = entry.TextBox;
        var isEditing = false;
        var isSettingText = false;

        void SetEditText(int editValue)
        {
            isSettingText = true;
            try
            {
                textBox.Text = DelayFormatter.ClampMilliseconds(editValue).ToString();
                entry.UnitText.Text = "ms";
            }
            finally
            {
                isSettingText = false;
            }
        }

        void SetDisplayText(int displayValue)
        {
            isSettingText = true;
            try
            {
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(displayValue));
            }
            finally
            {
                isSettingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) => { e.Handled = !e.Text.All(char.IsDigit); };

        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            SetEditText(currentValue());
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
        };

        textBox.TextChanged += (_, _) =>
        {
            if (!commitOnTextChanged || !isEditing || isSettingText || context.IsRefreshing())
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private static int GetDelayMinimum(MacroNode node)
    {
        var (min, _) = node.GetEffectiveDelayRange();
        return DelayFormatter.ClampMilliseconds(min);
    }

    private static int GetDelayMaximum(MacroNode node)
    {
        var (_, max) = node.GetEffectiveDelayRange();
        return DelayFormatter.ClampMilliseconds(max);
    }

    private static void SetDelayMinimum(MacroNode node, int value)
    {
        var (_, max) = node.GetEffectiveDelayRange();
        var min = DelayFormatter.ClampMilliseconds(value);
        node.SetDelayRange(min, DelayFormatter.ClampMilliseconds(max));
    }

    private static void SetDelayMaximum(MacroNode node, int value)
    {
        var (min, _) = node.GetEffectiveDelayRange();
        var max = DelayFormatter.ClampMilliseconds(value);
        node.SetDelayRange(DelayFormatter.ClampMilliseconds(min), max);
    }
}