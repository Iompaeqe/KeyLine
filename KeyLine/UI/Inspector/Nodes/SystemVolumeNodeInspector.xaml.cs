using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Common.EntryBlocks;
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