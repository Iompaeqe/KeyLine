using System.Collections.Generic;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class MouseScrollNodeInspector
{
    private NumberEntryBlock AmountEntry =>
        AmountRow.GetContent<NumberEntryBlock>()!;

    public MouseScrollNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            AmountEntry,
            read: InspectorFieldBinder.SingleInt(() => GetScrollAmount(node)),
            apply: value => context.CommitNodeValueChange(() => node.MouseScrollAmount = Math.Clamp(value, 1, 100)),
            min: 1,
            max: 100,
            tooltip: "How many scroll notches this node sends.",
            isEnabled: policy.CanEditMouseScrollAmount);
    }

    public void BindBatch(NodeInspectorContext context, IReadOnlyList<MacroNode> nodes)
    {
        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            AmountEntry,
            read: () => BatchValues.Read(nodes, GetScrollAmount),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    node.MouseScrollAmount = Math.Clamp(value, 1, 100);
            }),
            min: 1,
            max: 100,
            tooltip: "How many scroll notches this node sends. Applies to all selected nodes.",
            isEnabled: true);
    }

    private static int GetScrollAmount(MacroNode node) =>
        Math.Clamp(node.MouseScrollAmount <= 0 ? 1 : node.MouseScrollAmount, 1, 100);
}
