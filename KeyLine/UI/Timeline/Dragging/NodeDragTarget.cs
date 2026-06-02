using KeyLine.Domain;

namespace KeyLine.UI.Timeline;

public sealed class NodeDragTarget
{
    public required MacroNode RawInsertAnchor { get; init; }
    public required double CenterX { get; init; }
}
