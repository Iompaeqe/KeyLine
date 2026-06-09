using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;

namespace KeyLine.UI.Inspector.Nodes;

public partial class WindowReferenceConditionInspector
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

    public WindowReferenceConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        BindWindowSourceCombo(context, node, isEnabled);
        BindCustomTitleTextBox(context, node, isEnabled);

        RefreshDynamicState(node);
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

            RefreshDynamicState(node);
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

            RefreshDynamicState(node);
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

    private void RefreshDynamicState(MacroNode node)
    {
        CustomTitlePanel.Visibility =
            node.GetEffectiveWindowReference().Type == WindowReferenceType.CustomTitle
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private sealed record WindowReferenceTypeOption(string Label, WindowReferenceType Value)
    {
        public override string ToString() => Label;
    }
}
