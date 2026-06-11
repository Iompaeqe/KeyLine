using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class RandomChanceConditionInspector
{
    private StackPanel ChanceHost =>
        ChanceRow.GetContent<StackPanel>()!;

    private NumberEntryBlock ChanceEntry =>
        ChanceHost.Children.OfType<NumberEntryBlock>().First();

    public RandomChanceConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            ChanceEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Clamp(node.ConditionChancePercent, 0, 100)),
            apply: value => context.CommitNodeValueChange(() =>
                node.ConditionChancePercent = Math.Clamp(value, 0, 100)),
            min: 0,
            max: 100,
            tooltip: TooltipNotes.ConditionChance,
            isEnabled: isEnabled);
    }
}
