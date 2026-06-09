using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Common.Layout;
using KeyLine.UI.Timeline;

namespace KeyLine.UI.Inspector.Nodes;

public partial class RunMacroNodeInspector
{
    private ComboBox MacroCombo =>
        MacroRow.GetContent<ComboBox>()!;

    public RunMacroNodeInspector()
    {
        InitializeComponent();
    }

    public void Bind(NodeInspectorContext context, MacroNode node)
    {
        BindMacroCombo(
            context,
            node,
            currentMacroId: node.RunMacroId,
            setMacroId: value => node.RunMacroId = value,
            tooltip: "Macro from the active profile to run.",
            isEnabled: true,
            excludeActiveWorkspace: true);
    }

    private void BindMacroCombo(
        NodeInspectorContext context,
        MacroNode node,
        string currentMacroId,
        Action<string> setMacroId,
        string tooltip,
        bool isEnabled,
        bool excludeActiveWorkspace)
    {
        var canEdit = context.CanEditOption(isEnabled);
        var selectedId = currentMacroId?.Trim() ?? "";

        MacroCombo.ItemsSource = GetMacroOptions(context, selectedId, excludeActiveWorkspace);
        MacroCombo.SelectedValue = selectedId;
        MacroCombo.IsEnabled = canEdit;
        MacroCombo.ToolTip = tooltip;

        MacroCombo.SelectionChanged += (_, _) =>
        {
            if (context.IsRefreshing())
                return;

            if (MacroCombo.SelectedValue is not string value)
                return;

            value = value.Trim();

            if (string.Equals(value, node.RunMacroId?.Trim() ?? "", StringComparison.OrdinalIgnoreCase))
                return;

            context.CommitNodeChange(() => setMacroId(value));
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
            options.All(option => !string.Equals(option.Value, selectedMacroId, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new MacroOption("Missing Macro", selectedMacroId));
        }

        return options;
    }

    private static string? ResolveMacroName(NodeInspectorContext context, string macroId)
    {
        macroId = macroId?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(macroId))
            return null;

        return context.GetActiveProfileWorkspaces()
            .FirstOrDefault(workspace => string.Equals(
                workspace.Id,
                macroId,
                StringComparison.OrdinalIgnoreCase))
            ?.Name;
    }

    private sealed record MacroOption(string Label, string Value)
    {
        public override string ToString() => Label;
    }
}