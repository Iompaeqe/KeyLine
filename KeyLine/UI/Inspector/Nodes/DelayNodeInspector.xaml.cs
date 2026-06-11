using System.Collections.Generic;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;
using KeyLine.UI.Inspector.Fields;

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
        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            MinDelayEntry,
            read: InspectorFieldBinder.SingleInt(() => GetDelayMinimum(node)),
            apply: value => context.CommitNodeChange(() => SetDelayMinimum(node, value)),
            tooltip: $"{TooltipNotes.DelayNodeMinimum} Click to edit in milliseconds.",
            isEnabled: policy.CanEditDelay);

        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            MaxDelayEntry,
            read: InspectorFieldBinder.SingleInt(() => GetDelayMaximum(node)),
            apply: value => context.CommitNodeChange(() => SetDelayMaximum(node, value)),
            tooltip: $"{TooltipNotes.DelayNodeMaximum} Click to edit in milliseconds.",
            isEnabled: policy.CanEditDelay);
    }

    /// <summary>
    /// Batch mode: edits Min/Max delay across every selected delay node at once. Mixed values show the
    /// shared "Mixed" indicator and are left untouched unless the user types a value; each commit writes
    /// all nodes inside a single undo step (via CommitNodeChange). Same field behavior as single mode —
    /// only the read/apply differ.
    /// </summary>
    public void BindBatch(NodeInspectorContext context, IReadOnlyList<MacroNode> nodes)
    {
        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            MinDelayEntry,
            read: () => BatchValues.Read(nodes, GetDelayMinimum),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    SetDelayMinimum(node, value);
            }),
            tooltip: $"{TooltipNotes.DelayNodeMinimum} Click to edit in milliseconds. Applies to all selected nodes.",
            isEnabled: true);

        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            MaxDelayEntry,
            read: () => BatchValues.Read(nodes, GetDelayMaximum),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    SetDelayMaximum(node, value);
            }),
            tooltip: $"{TooltipNotes.DelayNodeMaximum} Click to edit in milliseconds. Applies to all selected nodes.",
            isEnabled: true);
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
