using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

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

        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            IntervalEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Max(1, node.ConditionLoopInterval)),
            apply: value => context.CommitNodeValueChange(() =>
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
        ModeCombo.ItemsSource = LoopModeOptions;

        InspectorFieldBinder.BindCombo<MacroConditionLoopMode>(
            context.FieldHost,
            ModeCombo,
            read: InspectorFieldBinder.SingleValue(() => node.ConditionLoopMode),
            apply: value => context.CommitNodeChange(() =>
            {
                node.ConditionLoopMode = value;
                node.ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval);
            }),
            isEnabled: isEnabled);
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

    private sealed record LoopModeOption(string Label, MacroConditionLoopMode Value)
    {
        public override string ToString() => Label;
    }
}
