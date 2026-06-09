using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;
using KeyLine.UI.Common.EntryBlocks;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector.Nodes;

public partial class SystemWindowReferenceNodeInspector
{
    private static readonly IReadOnlyList<WindowReferenceTypeOption> WindowReferenceTypeOptions =
    [
        new("Selected Target Window", WindowReferenceType.SelectedTarget),
        new("Focused Window", WindowReferenceType.FocusedWindow),
        new("Last Launched Window", WindowReferenceType.LastLaunchedWindow),
        new("Last Found Window", WindowReferenceType.LastFoundWindow),
        new("Custom Window Title", WindowReferenceType.CustomTitle)
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

        BindDelayEntry(
            entry: IntervalEntry,
            context: context,
            currentValue: () => Math.Clamp(
                node.SystemWaitPollIntervalMs <= 0 ? 250 : node.SystemWaitPollIntervalMs,
                50,
                10_000),
            commit: value => context.CommitNodeValueChange(() =>
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
        var canEdit = context.CanEditOption(isEnabled);
        var reference = node.GetEffectiveWindowReference();

        WindowSourceCombo.ItemsSource = WindowReferenceTypeOptions;
        WindowSourceCombo.SelectedValue = reference.Type;
        WindowSourceCombo.IsEnabled = canEdit;

        WindowSourceCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (WindowSourceCombo.SelectedItem is not WindowReferenceTypeOption option)
                return;

            if (option.Value == node.GetEffectiveWindowReference().Type)
                return;

            context.CommitNodeChange(() =>
            {
                var updated = node.GetEffectiveWindowReference();
                updated.Type = option.Value;
                node.WindowReference = updated;
                node.NormalizeWindowReference();
            });

            RefreshDynamicState(node, null);
        };
    }

    private void BindCustomTitleTextBox(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);
        var reference = node.GetEffectiveWindowReference();

        CustomTitleTextBox.Text = reference.CustomTitle;
        CustomTitleTextBox.IsEnabled = canEdit;

        void CommitWindowText()
        {
            if (context.IsRefreshing())
                return;

            var title = CustomTitleTextBox.Text.Trim();

            if (string.Equals(
                    title,
                    node.GetEffectiveWindowReference().CustomTitle,
                    StringComparison.Ordinal))
            {
                return;
            }

            context.CommitNodeChange(() =>
            {
                node.WindowReference = WindowReference.Custom(title);
                node.NormalizeWindowReference();
            });

            RefreshDynamicState(node, null);
        }

        CustomTitleTextBox.LostFocus += (_, _) => CommitWindowText();

        CustomTitleTextBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            CommitWindowText();
            Keyboard.ClearFocus();
            e.Handled = true;
        };
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

    private static void BindDelayEntry(
        TimeEntryBlock entry,
        NodeInspectorContext context,
        Func<int> currentValue,
        Action<int> commit,
        string tooltip,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);

        entry.ToolTip = $"{tooltip} Click to edit in milliseconds.";
        entry.IsEnabled = canEdit;
        entry.TextBox.IsEnabled = canEdit;
        entry.SetDisplay(DelayFormatter.ClampMilliseconds(currentValue()));

        var textBox = entry.TextBox;
        var isEditing = false;
        var isSettingText = false;

        void SetEditText(int editValue)
        {
            isSettingText = true;
            try
            {
                textBox.Text = DelayFormatter.ClampMilliseconds(editValue).ToString();
                entry.UnitText.Text = "ms";
            }
            finally
            {
                isSettingText = false;
            }
        }

        void SetDisplayText(int displayValue)
        {
            isSettingText = true;
            try
            {
                entry.SetDisplay(DelayFormatter.ClampMilliseconds(displayValue));
            }
            finally
            {
                isSettingText = false;
            }
        }

        textBox.PreviewTextInput += (_, e) =>
        {
            e.Handled = !e.Text.All(char.IsDigit);
        };

        textBox.GotKeyboardFocus += (_, _) =>
        {
            isEditing = true;
            SetEditText(currentValue());
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) =>
        {
            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
        };

        textBox.TextChanged += (_, _) =>
        {
            if (!isEditing || isSettingText || context.IsRefreshing())
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
        };

        textBox.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;

            InspectorCommitService.CommitDelayText(entry, currentValue(), commit);
            isEditing = false;
            SetDisplayText(currentValue());
            Keyboard.ClearFocus();
            e.Handled = true;
        };
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