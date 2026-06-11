using KeyLine.Domain;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;

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
        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            EveryEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Max(1, node.ConditionTimePassedMs)),
            apply: value => context.CommitNodeValueChange(() =>
                node.ConditionTimePassedMs = Math.Max(1, value)),
            tooltip: $"{TimePassedTooltip} Click to edit in milliseconds.",
            isEnabled: isEnabled);
    }
}
