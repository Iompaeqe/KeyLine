using System.Windows.Controls;
using KeyLine.Domain;

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
        var canEdit = context.CanEditOption(isEnabled);
        var selectedId = node.ConditionMacroId?.Trim() ?? "";

        MacroCombo.ItemsSource = GetMacroOptions(context, selectedId, excludeActiveWorkspace: true);
        MacroCombo.SelectedValue = selectedId;
        MacroCombo.IsEnabled = canEdit;

        MacroCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (MacroCombo.SelectedValue is not string value)
                return;

            value = value.Trim();

            if (string.Equals(
                    value,
                    node.ConditionMacroId?.Trim() ?? "",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            context.CommitNodeChange(() => node.ConditionMacroId = value);
        };
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