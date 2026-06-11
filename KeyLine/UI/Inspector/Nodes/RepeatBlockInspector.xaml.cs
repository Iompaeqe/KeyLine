using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class RepeatBlockInspector
{
    private NumberEntryBlock CountEntry =>
        CountRow.GetContent<NumberEntryBlock>()!;

    private TextBlock InsideValue =>
        InsideRow.GetContent<TextBlock>()!;

    public RepeatBlockInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode repeatStart, int blockNodeCount)
    {
        InspectorFieldBinder.BindNumber(
            context.FieldHost,
            CountEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Max(1, repeatStart.RepeatCount)),
            apply: value => context.CommitNodeValueChange(() => repeatStart.RepeatCount = Math.Max(1, value)),
            min: 1,
            max: null,
            tooltip: TooltipNotes.RepeatCount,
            isEnabled: true);

        InsideValue.Text = Math.Max(0, blockNodeCount - 2).ToString();
    }
}
