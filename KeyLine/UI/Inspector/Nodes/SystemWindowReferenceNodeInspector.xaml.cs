using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Fields;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector.Nodes;

public partial class SystemWindowReferenceNodeInspector
{
    private static readonly IReadOnlyList<WindowReferenceTypeOption> WindowReferenceTypeOptions =
    [
        new("Target", WindowReferenceType.SelectedTarget),
        new("Focused", WindowReferenceType.FocusedWindow),
        new("Last Launched", WindowReferenceType.LastLaunchedWindow),
        new("Last Found", WindowReferenceType.LastFoundWindow),
        new("Custom Title", WindowReferenceType.CustomTitle)
    ];

    private ComboBox WindowSourceCombo =>
        WindowSourceRow.GetContent<ComboBox>()!;

    private TimeEntryBlock IntervalEntry =>
        IntervalRow.GetContent<TimeEntryBlock>()!;

    public SystemWindowReferenceNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(
        NodeInspectorContext context,
        MacroNode node,
        NodeInspectorPolicy policy,
        SystemWindowReferenceInspectorMode mode)
    {
        var tooltip = GetWindowSourceTooltip(mode);
        var canEdit = mode switch
        {
            SystemWindowReferenceInspectorMode.WaitUntilWindowOpens => policy.CanEditSystemWindowWait,
            SystemWindowReferenceInspectorMode.SelectTargetWindow => policy.CanEditSystemTargetWindow,
            SystemWindowReferenceInspectorMode.FocusWindow => policy.CanEditSystemFocusWindow,
            _ => false
        };

        WindowSourceRow.RowToolTip = tooltip;
        WindowSourceCombo.ToolTip = tooltip;

        BindWindowSourceCombo(context, node, canEdit);
        BindCustomTitleTextBox(context, node, canEdit);

        InspectorFieldBinder.BindDelay(
            context.FieldHost,
            IntervalEntry,
            read: InspectorFieldBinder.SingleInt(() => Math.Clamp(
                node.SystemWaitPollIntervalMs <= 0 ? 250 : node.SystemWaitPollIntervalMs,
                50,
                10_000)),
            apply: value => context.CommitNodeValueChange(() =>
                node.SystemWaitPollIntervalMs = Math.Clamp(value, 50, 10_000)),
            tooltip: "How often KeyLine checks for the window title.",
            isEnabled: canEdit);

        RefreshDynamicState(node, mode);
    }

    private void BindWindowSourceCombo(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        WindowSourceCombo.ItemsSource = WindowReferenceTypeOptions;

        InspectorFieldBinder.BindCombo<WindowReferenceType>(
            context.FieldHost,
            WindowSourceCombo,
            read: InspectorFieldBinder.SingleValue(() => node.GetEffectiveWindowReference().Type),
            apply: value => context.CommitNodeChange(() =>
            {
                var updated = node.GetEffectiveWindowReference();
                updated.Type = value;
                node.WindowReference = updated;
                node.NormalizeWindowReference();
            }),
            isEnabled: isEnabled);
    }

    private void BindCustomTitleTextBox(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        InspectorFieldBinder.BindText(
            context.FieldHost,
            CustomTitleTextBox,
            read: InspectorFieldBinder.SingleText(() => node.GetEffectiveWindowReference().CustomTitle),
            apply: value => context.CommitNodeChange(() =>
            {
                node.WindowReference = WindowReference.Custom(value.Trim());
                node.NormalizeWindowReference();
            }),
            isEnabled: isEnabled);
    }

    private void RefreshDynamicState(
        MacroNode node,
        SystemWindowReferenceInspectorMode? mode)
    {
        CustomTitlePanel.Visibility =
            node.GetEffectiveWindowReference().Type == WindowReferenceType.CustomTitle
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (mode.HasValue)
        {
            IntervalRow.Visibility =
                mode.Value == SystemWindowReferenceInspectorMode.WaitUntilWindowOpens
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }

    private static string GetWindowSourceTooltip(SystemWindowReferenceInspectorMode mode) =>
        mode switch
        {
            SystemWindowReferenceInspectorMode.WaitUntilWindowOpens => "Window source to wait for.",
            SystemWindowReferenceInspectorMode.SelectTargetWindow => "Window source to use as the runtime playback target.",
            SystemWindowReferenceInspectorMode.FocusWindow => "Window source to focus and use for following input nodes.",
            _ => "Window source."
        };

    private sealed record WindowReferenceTypeOption(string Label, WindowReferenceType Value)
    {
        public override string ToString() => Label;
    }
}

public enum SystemWindowReferenceInspectorMode
{
    WaitUntilWindowOpens,
    SelectTargetWindow,
    FocusWindow
}