using KeyLine.Domain;

namespace KeyLine.UI.Nodes;

public abstract class BlockNodeBase : NodeBase
{
    protected bool IsBlockStart => Node?.Type == MacroNodeType.RepeatStart;

    protected bool IsBlockEnd => Node?.Type == MacroNodeType.RepeatEnd;
}
