using KeySpammer.Domain;

namespace KeySpammer.UI.Timeline;

public sealed class StepPreviewSlot
{
    public required List<MacroStep> RawItems { get; init; }
    public required double CenterX { get; init; }
    public required bool IsDraggedSlot { get; init; }
}
