using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public abstract class BlockNodeBase : NodeBase
{
    protected bool IsBlockStart => Node?.Type is MacroNodeType.RepeatStart or MacroNodeType.ConditionStart;

    protected bool IsBlockEnd => Node?.Type is MacroNodeType.RepeatEnd or MacroNodeType.ConditionEnd;
}
