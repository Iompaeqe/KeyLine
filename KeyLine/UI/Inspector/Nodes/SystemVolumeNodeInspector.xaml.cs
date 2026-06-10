using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Inspector.Batch;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector.Nodes;

public partial class SystemVolumeNodeInspector
{
    private static readonly IReadOnlyList<VolumeActionOption> VolumeActionOptions =
    [
        new("Volume Up", SystemVolumeAction.VolumeUp),
        new("Volume Down", SystemVolumeAction.VolumeDown),
        new("Mute Toggle", SystemVolumeAction.MuteToggle),
        new("Mute", SystemVolumeAction.Mute),
        new("Unmute", SystemVolumeAction.Unmute),
        new("Set Volume %", SystemVolumeAction.SetVolumePercent)
    ];
    private ComboBox ActionCombo =>
        ActionRow.GetContent<ComboBox>()!;

    private StackPanel VolumeHost =>
        VolumeRow.GetContent<StackPanel>()!;

    private NumberEntryBlock VolumeEntry =>
        VolumeHost.Children.OfType<NumberEntryBlock>().First();

    public SystemVolumeNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        BindActionCombo(context, node, policy.CanEditVolumeControl);

        BindNumberEntry(
            entry: VolumeEntry,
            context: context,
            value: Math.Clamp(node.SystemVolumePercent, 0, 100),
            commit: value => context.CommitNodeValueChange(() =>
                node.SystemVolumePercent = Math.Clamp(value, 0, 100)),
            min: 0,
            max: 100,
            tooltip: "Set the system output volume percentage.",
            isEnabled: policy.CanEditVolumeControl);

        RefreshDynamicState(node);
    }

    public void BindBatch(NodeInspectorContext context, IReadOnlyList<MacroNode> nodes)
    {
        BindBatchActionCombo(context, nodes);

        BatchEntryBinder.BindNumber(
            entry: VolumeEntry,
            context: context,
            read: () => BatchValues.Read(nodes, node => Math.Clamp(node.SystemVolumePercent, 0, 100)),
            apply: value => context.CommitNodeChange(() =>
            {
                foreach (var node in nodes)
                    node.SystemVolumePercent = Math.Clamp(value, 0, 100);
            }),
            min: 0,
            max: 100,
            tooltip: "Set the system output volume percentage.",
            isEnabled: true);

        // The volume % row only makes sense when every selected node uses Set Volume %.
        VolumeRow.Visibility = nodes.All(node => node.SystemVolumeAction == SystemVolumeAction.SetVolumePercent)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // Sentinel "Mixed" row shown as the dropdown's selected item when the nodes' actions differ.
    private static readonly VolumeActionOption MixedActionOption =
        new(BatchUi.IndicatorText, (SystemVolumeAction)(-1));

    private void BindBatchActionCombo(NodeInspectorContext context, IReadOnlyList<MacroNode> nodes)
    {
        var canEdit = context.CanEditOption(true);
        ActionCombo.IsEnabled = canEdit;

        var batch = BatchValues.Read(nodes, node => node.SystemVolumeAction);
        if (batch.HasMixedValue)
        {
            var items = new List<VolumeActionOption> { MixedActionOption };
            items.AddRange(VolumeActionOptions);
            ActionCombo.ItemsSource = items;
            ActionCombo.SelectedItem = MixedActionOption;
            ActionCombo.ToolTip = "Mixed — choose an action to apply it to all selected nodes.";
        }
        else
        {
            ActionCombo.ItemsSource = VolumeActionOptions;
            ActionCombo.SelectedValue = batch.Value;
            ActionCombo.ToolTip = null;
        }

        ActionCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (ActionCombo.SelectedItem is not VolumeActionOption option || ReferenceEquals(option, MixedActionOption))
                return;

            var current = BatchValues.Read(nodes, node => node.SystemVolumeAction);
            if (!current.HasMixedValue && current.Value == option.Value)
                return;

            var selectedAction = option.Value;

            // Defer: committing rebuilds the inspector (replacing this ComboBox). Doing that inside the
            // ComboBox's own SelectionChanged drops the pick, so let the selection settle first.
            Dispatcher.BeginInvoke(new Action(() =>
                context.CommitNodeChange(() =>
                {
                    foreach (var node in nodes)
                    {
                        node.SystemVolumeAction = selectedAction;
                        node.SystemVolumePercent = Math.Clamp(node.SystemVolumePercent, 0, 100);
                    }
                })));
        };
    }

    private void BindActionCombo(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        ActionCombo.ItemsSource = VolumeActionOptions;
        ActionCombo.SelectedValue = node.SystemVolumeAction;
        ActionCombo.IsEnabled = canEdit;

        ActionCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (ActionCombo.SelectedItem is not VolumeActionOption option)
                return;

            if (option.Value == node.SystemVolumeAction)
                return;

            context.CommitNodeChange(() =>
            {
                node.SystemVolumeAction = option.Value;
                node.SystemVolumePercent = Math.Clamp(node.SystemVolumePercent, 0, 100);
            });

            RefreshDynamicState(node);
        };
    }

    private void RefreshDynamicState(MacroNode node)
    {
        VolumeRow.Visibility = node.SystemVolumeAction == SystemVolumeAction.SetVolumePercent
            ? Visibility.Visible
            : Visibility.Collapsed;
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

    private sealed record VolumeActionOption(string Label, SystemVolumeAction Value)
    {
        public override string ToString() => Label;
    }
}