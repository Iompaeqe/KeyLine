using KeySpammer.Domain;

namespace KeySpammer.UI.Timeline;

public sealed class StepDragTarget
{
    public required MacroStep RawInsertAnchor { get; init; }
    public required double CenterX { get; init; }
}
