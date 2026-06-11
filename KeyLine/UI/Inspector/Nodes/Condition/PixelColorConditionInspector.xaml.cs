using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class PixelColorConditionInspector
{
    private MultiValueEntryBlock PositionBlock =>
        PositionRow.GetContent<MultiValueEntryBlock>()!;

    private MultiValueEntryBlock ColorBlock =>
        ColorRow.GetContent<MultiValueEntryBlock>()!;

    private NumberEntryBlock ToleranceEntry =>
        ToleranceRow.GetContent<NumberEntryBlock>()!;

    public PixelColorConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        PositionBlock.SetFields(
            [
                new MultiValueEntryField(
                    "X",
                    Math.Max(0, node.ConditionPixelX),
                    value => context.CommitNodeChange(() =>
                        node.ConditionPixelX = Math.Max(0, value))),

                new MultiValueEntryField(
                    "Y",
                    Math.Max(0, node.ConditionPixelY),
                    value => context.CommitNodeChange(() =>
                        node.ConditionPixelY = Math.Max(0, value)))
            ],
            context.IsRefreshing,
            canEdit,
            TooltipNotes.ConditionPixelPosition);

        ColorBlock.SetFields(
            [
                new MultiValueEntryField(
                    "R",
                    Math.Clamp(node.ConditionPixelRed, 0, 255),
                    value => context.CommitNodeChange(() =>
                        node.ConditionPixelRed = Math.Clamp(value, 0, 255)),
                    Max: 255),

                new MultiValueEntryField(
                    "G",
                    Math.Clamp(node.ConditionPixelGreen, 0, 255),
                    value => context.CommitNodeChange(() =>
                        node.ConditionPixelGreen = Math.Clamp(value, 0, 255)),
                    Max: 255),

                new MultiValueEntryField(
                    "B",
                    Math.Clamp(node.ConditionPixelBlue, 0, 255),
                    value => context.CommitNodeChange(() =>
                        node.ConditionPixelBlue = Math.Clamp(value, 0, 255)),
                    Max: 255)
            ],
            context.IsRefreshing,
            canEdit,
            TooltipNotes.ConditionPixelColor);

        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            ToleranceEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Clamp(node.ConditionPixelTolerance, 0, 255)),
            apply: value => context.CommitNodeValueChange(() =>
                node.ConditionPixelTolerance = Math.Clamp(value, 0, 255)),
            min: 0,
            max: 255,
            tooltip: TooltipNotes.ConditionPixelTolerance,
            isEnabled: isEnabled);

        PickPixelButton.IsEnabled = canEdit;
        PickPixelButton.Click += async (_, _) =>
        {
            context.SaveUndoSnapshot();
            await context.PickConditionPixelAsync(node);
            context.RefreshInspector();
        };
    }
}