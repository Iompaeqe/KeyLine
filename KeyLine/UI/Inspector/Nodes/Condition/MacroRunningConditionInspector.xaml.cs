using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Inspector.Fields;

namespace KeyLine.UI.Inspector.Nodes;

public partial class MacroRunningConditionInspector
{
    private ComboBox MacroCombo =>
        MacroRow.GetContent<ComboBox>()!;

    public MacroRunningConditionInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node, bool isEnabled)
    {
        var selectedId = node.ConditionMacroId?.Trim() ?? "";

        MacroCombo.ItemsSource = GetMacroOptions(context, selectedId, excludeActiveWorkspace: true);

        InspectorFieldBinder.BindCombo<string>(
            context.FieldHost,
            MacroCombo,
            read: InspectorFieldBinder.SingleValue(() => node.ConditionMacroId?.Trim() ?? ""),
            apply: value => context.CommitNodeChange(() => node.ConditionMacroId = value.Trim()),
            isEnabled: isEnabled,
            comparer: StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<MacroOption> GetMacroOptions(
        NodeInspectorContext context,
        string selectedMacroId,
        bool excludeActiveWorkspace)
    {
        selectedMacroId = selectedMacroId?.Trim() ?? "";

        var activeWorkspace = context.GetActiveWorkspace();

        var options = new List<MacroOption>
        {
            new("Select macro", "")
        };

        var workspaces = context.GetActiveProfileWorkspaces()
            .Where(workspace =>
                !excludeActiveWorkspace ||
                !string.Equals(workspace.Id, activeWorkspace.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var workspace in workspaces)
            options.Add(new MacroOption(workspace.Name, workspace.Id));

        if (!string.IsNullOrWhiteSpace(selectedMacroId) &&
            options.All(option => !string.Equals(
                option.Value,
                selectedMacroId,
                StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new MacroOption("Missing Macro", selectedMacroId));
        }

        return options;
    }

    private sealed record MacroOption(string Label, string Value)
    {
        public override string ToString() => Label;
    }
}