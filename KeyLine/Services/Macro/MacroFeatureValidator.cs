using KeyLine.Domain;
using KeyLine.Services.Features;

namespace KeyLine.Services.Macro;

public sealed class MacroFeatureValidator
{
    private readonly FeatureGate _featureGate;

    public MacroFeatureValidator(FeatureGate featureGate)
    {
        _featureGate = featureGate;
    }

    public MacroFeatureValidationResult ValidateWorkspace(MacroWorkspace workspace)
    {
        var result = new MacroFeatureValidationResult();

        if (!string.IsNullOrWhiteSpace(workspace.TargetWindowSearchName) &&
            !_featureGate.IsEnabled(FeatureId.AutoWindow))
        {
            AddUnique(result.Errors, CreateDisabledNodeMessage(FeatureId.AutoWindow));
        }

        foreach (var timeline in workspace.Document.Timelines)
        {
            foreach (var node in timeline.Nodes)
            {
                var feature = GetRequiredFeatureForNode(node);
                if (feature == null || _featureGate.IsEnabled(feature.Value))
                    continue;

                AddUnique(result.Errors, CreateDisabledNodeMessage(feature.Value));
            }
        }

        return result;
    }

    public MacroFeatureValidationResult ValidateWorkspaces(IEnumerable<MacroWorkspace> workspaces)
    {
        var result = new MacroFeatureValidationResult();

        foreach (var workspace in workspaces)
        {
            var workspaceResult = ValidateWorkspace(workspace);
            foreach (var error in workspaceResult.Errors)
                AddUnique(result.Errors, error);

            foreach (var warning in workspaceResult.Warnings)
                AddUnique(result.Warnings, warning);
        }

        return result;
    }

    public static string FormatErrors(MacroFeatureValidationResult result)
    {
        return string.Join(Environment.NewLine, result.Errors.Distinct(StringComparer.Ordinal));
    }

    public static FeatureId? GetRequiredFeatureForNode(MacroNode node)
    {
        return node.Type switch
        {
            MacroNodeType.RepeatStart or MacroNodeType.RepeatEnd => FeatureId.RepeatBlocks,
            MacroNodeType.ConditionStart or MacroNodeType.ConditionEnd => FeatureId.ConditionBlocks,
            MacroNodeType.SystemOpenLaunch or
                MacroNodeType.SystemVolumeControl or
                MacroNodeType.SystemWaitUntilWindowOpens or
                MacroNodeType.SystemSelectTargetWindow or
                MacroNodeType.SystemFocusWindow => FeatureId.SystemNodes,
            _ => null
        };
    }

    private string CreateDisabledNodeMessage(FeatureId feature)
    {
        return feature switch
        {
            FeatureId.RepeatBlocks =>
                "This macro contains Repeat Blocks, but Repeat Blocks are disabled in this version.",
            FeatureId.ConditionBlocks =>
                "This macro contains Condition Blocks, but Condition Blocks are disabled in this version.",
            FeatureId.AutoWindow =>
                "This macro uses auto window targeting, but auto window targeting is disabled in this version.",
            FeatureId.SystemNodes =>
                "This macro contains System nodes, but System nodes are disabled in this version.",
            _ =>
                $"This macro contains a disabled feature: {feature}."
        };
    }

    private static void AddUnique(ICollection<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.Ordinal))
            target.Add(value);
    }
}
