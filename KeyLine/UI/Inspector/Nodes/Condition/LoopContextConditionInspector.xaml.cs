using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;

namespace KeyLine.UI.Inspector.Nodes;

public partial class LoopContextConditionInspector
{
    private static readonly IReadOnlyList<LoopModeOption> LoopModeOptions =
    [
        new("First loop", MacroConditionLoopMode.FirstLoop),
        new("Last loop", MacroConditionLoopMode.LastLoop),
        new("Every N loops", MacroConditionLoopMode.EveryNLoops),
        new("First repeat", MacroConditionLoopMode.FirstRepeat),
        new("Last repeat", MacroConditionLoopMode.LastRepeat),
        new("Every N repeats", MacroConditionLoopMode.EveryNRepeats)
    ];

    private ComboBox ModeCombo =>
        ModeRow.GetContent<ComboBox>()!;

    private StackPanel IntervalHost =>
        IntervalRow.GetContent<StackPanel>()!;

    private NumberEntryBlock IntervalEntry =>
        IntervalHost.Children.OfType<NumberEntryBlock>().First();

    private TextBlock IntervalSuffix =>
        IntervalHost.Children.OfType<TextBlock>().First();

    public LoopContextConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        BindModeCombo(context, node, isEnabled);

        BindNumberEntry(
            entry: IntervalEntry,
            context: context,
            value: Math.Max(1, node.ConditionLoopInterval),
            commit: value => context.CommitNodeValueChange(() =>
                node.ConditionLoopInterval = Math.Max(1, value)),
            min: 1,
            max: null,
            tooltip: TooltipNotes.ConditionLoopInterval,
            isEnabled: isEnabled);

        RefreshDynamicState(node);
    }

    private void BindModeCombo(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        ModeCombo.ItemsSource = LoopModeOptions;
        ModeCombo.SelectedValue = node.ConditionLoopMode;
        ModeCombo.IsEnabled = canEdit;

        ModeCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (ModeCombo.SelectedItem is not LoopModeOption option)
                return;

            if (option.Value == node.ConditionLoopMode)
                return;

            context.CommitNodeChange(() =>
            {
                node.ConditionLoopMode = option.Value;
                node.ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval);
            });

            RefreshDynamicState(node);
        };
    }

    private void RefreshDynamicState(MacroNode node)
    {
        IntervalRow.Visibility = RequiresLoopInterval(node.ConditionLoopMode)
            ? Visibility.Visible
            : Visibility.Collapsed;

        IntervalSuffix.Text = node.ConditionLoopMode == MacroConditionLoopMode.EveryNRepeats
            ? "repeats"
            : "loops";
    }

    private static bool RequiresLoopInterval(MacroConditionLoopMode mode)
    {
        return mode is MacroConditionLoopMode.EveryNLoops
            or MacroConditionLoopMode.EveryNRepeats;
    }

    private static void BindNumberEntry(
        NumberEntryBlock entry,
        NodeInspectorContext context,
        int value,
        Action<int> commit,
        int min,
        int? max,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.IsEnabled = canEdit;
        entry.ToolTip = tooltip;

        var textBox = entry.TextBox;
        textBox.Text = value.ToString();
        textBox.IsEnabled = canEdit;

        var committedValue = value;
        var isCommittingText = false;

        void CommitText()
        {
            if (isCommittingText)
                return;

            isCommittingText = true;
            try
            {
                committedValue = InspectorCommitService.CommitNumberText(
                    textBox,
                    committedValue,
                    commit,
                    min,
                    max,
                    context.IsRefreshing());
            }
            finally
            {
                isCommittingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(char.IsDigit);
        };

        textBox.GotKeyboardFocus += (_, _) =>
        {
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            CommitText();
        };

        textBox.TextChanged += (_, _) =>
        {
            CommitText();
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
    }

    private sealed record LoopModeOption(string Label, MacroConditionLoopMode Value)
    {
        public override string ToString() => Label;
    }
}