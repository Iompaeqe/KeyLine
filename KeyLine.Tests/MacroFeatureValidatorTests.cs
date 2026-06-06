using KeyLine.Domain;
using KeyLine.Services.Features;
using KeyLine.Services.Macro;

namespace KeyLine.Tests;

public sealed class MacroFeatureValidatorTests
{
    [Fact]
    public void ValidateWorkspace_BlocksAutoWindowWhenFeatureDisabled()
    {
        var workspace = new MacroWorkspace
        {
            TargetWindowSearchName = "Notepad"
        };

        var validator = new MacroFeatureValidator(new FeatureGate(new FeatureConfig
        {
            AutoWindow = FeatureState.DisabledVisible
        }));

        var result = validator.ValidateWorkspace(workspace);

        Assert.False(result.CanRun);
        Assert.Contains(
            "This macro uses auto window targeting, but auto window targeting is disabled in this version.",
            result.Errors);
    }

    [Fact]
    public void ValidateWorkspace_BlocksSystemNodesWhenFeatureDisabled()
    {
        var workspace = new MacroWorkspace();
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemOpenLaunch
        });

        var validator = new MacroFeatureValidator(new FeatureGate(new FeatureConfig
        {
            SystemNodes = FeatureState.DisabledVisible
        }));

        var result = validator.ValidateWorkspace(workspace);

        Assert.False(result.CanRun);
        Assert.Contains(
            "This macro contains System nodes, but System nodes are disabled in this version.",
            result.Errors);
    }

    [Fact]
    public void GetRequiredFeatureForNode_ReturnsSystemNodesForSystemNodeTypes()
    {
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.SystemOpenLaunch }));
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.SystemVolumeControl }));
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.SystemWaitUntilWindowOpens }));
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.SystemSelectTargetWindow }));
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.SystemFocusWindow }));
        Assert.Equal(
            FeatureId.SystemNodes,
            MacroFeatureValidator.GetRequiredFeatureForNode(new MacroNode { Type = MacroNodeType.RunMacro }));
    }
}
