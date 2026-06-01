using MacroSpammer.Domain;

namespace MacroSpammer;

public partial class MainWindow
{
    private List<MacroNode> GetRawStepsForDisplayStep(IReadOnlyCollection<MacroNode> rawSteps, MacroNode node)
    {
        if (node.IsSyntheticDisplayNode)
        {
            return node.SourceNodes
                .Where(rawSteps.Contains)
                .ToList();
        }

        return rawSteps.Contains(node)
            ? new List<MacroNode> { node }
            : new List<MacroNode>();
    }
}
