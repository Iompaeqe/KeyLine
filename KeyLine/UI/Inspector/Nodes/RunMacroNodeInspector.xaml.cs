using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.UI.Inspector.Fields;

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
        var selectedId = currentMacroId?.Trim() ?? "";

        MacroCombo.ItemsSource = GetMacroOptions(context, selectedId, excludeActiveWorkspace);
        MacroCombo.ToolTip = tooltip;

        InspectorFieldBinder.BindCombo<string>(
            context.FieldHost,
            MacroCombo,
            read: InspectorFieldBinder.SingleValue(() => node.RunMacroId?.Trim() ?? ""),
            apply: value => context.CommitNodeChange(() => setMacroId(value.Trim())),
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