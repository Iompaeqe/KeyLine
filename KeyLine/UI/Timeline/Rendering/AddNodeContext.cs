using KeyLine.Domain;

namespace KeyLine.UI.Timeline;

/// <summary>
/// Tag carried by an "add" button so a click knows which timeline (and optional insert anchor for
/// a block's leading add) it belongs to. Created by the renderer, resolved by the host's add menu.
/// </summary>
public sealed class AddNodeContext
{
    public required MacroTimeline Timeline { get; init; }
    public MacroNode? RawInsertAnchor { get; init; }
}
