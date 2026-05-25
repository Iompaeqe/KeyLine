using MacroSpammer.Domain;

namespace MacroSpammer.UI.Timeline;

public sealed class StepDragTarget
{
    public required MacroStep RawInsertAnchor { get; init; }
    public required double CenterX { get; init; }
}
