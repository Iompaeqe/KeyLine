using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Input;

namespace KeyLine.UI.Inspector.Nodes;

public partial class KeyToggleNodeInspector
{
    private static readonly IReadOnlyList<ToggleModeOption> ToggleKeyModeOptions =
    [
        new("Normal", ToggleKeyMode.Normal),
        new("Toggle", ToggleKeyMode.Toggle),
        new("Toggle On", ToggleKeyMode.ToggleOn),
        new("Toggle Off", ToggleKeyMode.ToggleOff)
    ];

    private TextBlock KeyText =>
        KeyRow.GetContent<TextBlock>()!;

    private ComboBox ToggleModeCombo =>
        ToggleModeRow.GetContent<ComboBox>()!;

    public KeyToggleNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, NodeInspectorPolicy policy)
    {
        KeyText.Text = string.IsNullOrWhiteSpace(node.KeyName)
            ? "-"
            : node.KeyName;

        BindToggleModeCombo(context, node, policy.CanEditToggleKeyMode);
    }

    private void BindToggleModeCombo(
        NodeInspectorContext context,
        MacroNode node,
        bool isEnabled)
    {
        var canEdit = context.CanEditOption(isEnabled);
        var currentValue = GetToggleKeyMode(node);

        ToggleModeCombo.ItemsSource = ToggleKeyModeOptions;
        ToggleModeCombo.SelectedValue = currentValue;
        ToggleModeCombo.IsEnabled = canEdit;

        ToggleModeCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (ToggleModeCombo.SelectedItem is not ToggleModeOption option)
                return;

            if (option.Value == GetToggleKeyMode(node))
                return;

            context.CommitNodeChange(() => SetToggleKeyMode(node, option.Value));
        };
    }

    private static ToggleKeyMode GetToggleKeyMode(MacroNode node)
    {
        if (!node.IsSyntheticDisplayNode)
            return node.ToggleKeyMode;

        var source = GetToggleKeySourceNodes(node)
            .FirstOrDefault(step => step.ToggleKeyMode != ToggleKeyMode.Normal)
            ?? GetToggleKeySourceNodes(node).FirstOrDefault();

        return source?.ToggleKeyMode ?? ToggleKeyMode.Normal;
    }

    private static void SetToggleKeyMode(MacroNode node, ToggleKeyMode mode)
    {
        if (!node.IsSyntheticDisplayNode)
        {
            node.ToggleKeyMode = mode;
            return;
        }

        var sources = GetToggleKeySourceNodes(node).ToList();

        if (sources.Count == 0)
            return;

        var primary = sources.FirstOrDefault(source => source.Type == MacroNodeType.KeyDown) ?? sources[0];

        foreach (var source in sources)
            source.ToggleKeyMode = ReferenceEquals(source, primary)
                ? mode
                : ToggleKeyMode.Normal;
    }

    private static IEnumerable<MacroNode> GetToggleKeySourceNodes(MacroNode node)
    {
        if (!node.IsSyntheticDisplayNode)
        {
            return ToggleKeyService.IsToggleKey(node.VirtualKey)
                ? new[] { node }
                : Array.Empty<MacroNode>();
        }

        return node.SourceNodes.Where(source => ToggleKeyService.IsToggleKey(source.VirtualKey));
    }

    private sealed record ToggleModeOption(string Label, ToggleKeyMode Value)
    {
        public override string ToString() => Label;
    }
}