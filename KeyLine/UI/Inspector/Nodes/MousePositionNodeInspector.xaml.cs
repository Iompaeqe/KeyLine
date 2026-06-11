using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class MousePositionNodeInspector
{
    private NumberEntryBlock XEntry =>
        XRow.GetContent<NumberEntryBlock>()!;

    private NumberEntryBlock YEntry =>
        YRow.GetContent<NumberEntryBlock>()!;

    public MousePositionNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            XEntry,
            read: InspectorFieldBinder.SingleInt(() => node.MouseX),
            apply: value => context.CommitNodeValueChange(() => node.MouseX = value),
            min: 0,
            max: null,
            tooltip: "Coordinate value.",
            isEnabled: policy.CanEditMousePosition);

        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            YEntry,
            read: InspectorFieldBinder.SingleInt(() => node.MouseY),
            apply: value => context.CommitNodeValueChange(() => node.MouseY = value),
            min: 0,
            max: null,
            tooltip: "Coordinate value.",
            isEnabled: policy.CanEditMousePosition);

        PickPointButton.IsEnabled = context.CanEditOption(policy.CanPickMousePosition);
        PickPointButton.Click += async (_, _) =>
        {
            context.SaveUndoSnapshot();
            await context.PickMouseCoordinatesForNodeAsync(node);
            context.RefreshInspector();
        };
    }
}
