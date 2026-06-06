using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;

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