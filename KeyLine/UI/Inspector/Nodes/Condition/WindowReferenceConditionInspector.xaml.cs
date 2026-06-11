using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Inspector.Fields;

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
