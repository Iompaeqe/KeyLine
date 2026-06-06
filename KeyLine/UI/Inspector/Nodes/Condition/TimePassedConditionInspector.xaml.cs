using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class TimePassedConditionInspector
{
    private const string TimePassedTooltip =
        "Amount of active playback time that must pass before this condition succeeds.";

    private TimeEntryBlock EveryEntry =>
        EveryRow.GetContent<TimeEntryBlock>()!;

    public TimePassedConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        BindDelayEntry(
            entry: EveryEntry,
            context: context,
            currentValue: () => Math.Max(1, node.ConditionTimePassedMs),
            commit: value => context.CommitNodeValueChange(() =>
                node.ConditionTimePassedMs = Math.Max(1, value)),
            tooltip: TimePassedTooltip,
            isEnabled: isEnabled);
    }

    private static void BindDelayEntry(
        TimeEntryBlock entry,
        NodeInspectorContext context,
        Func<int> currentValue,
        Action<int> commit,
        string tooltip,
        bool isEnabled)
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

        textBox.PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(char.IsDigit);
        };

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
            if (!isEditing || isSettingText || context.IsRefreshing())
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
}