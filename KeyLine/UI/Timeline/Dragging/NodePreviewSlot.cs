using KeyLine.Domain;

namespace KeyLine.UI.Timeline;

public sealed class NodePreviewSlot
{
    public required MacroNode DisplayNode { get; init; }
    public required List<MacroNode> RawItems { get; init; }
    public required double Width { get; init; }
    public required bool IsDraggedSlot { get; init; }

    public double Left { get; set; }
    public double CenterX => Left + (Width / 2.0);
}