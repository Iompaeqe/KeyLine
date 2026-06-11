using System;
using KeyLine.Domain;

namespace KeyLine.UI.Inspector;

/// <summary>
/// Targeted timeline-refresh callbacks the inspector commit pipeline uses so a model edit updates
/// only the affected visuals instead of rebuilding the whole timeline. Supplied by the host, which
/// forwards them to the renderer's tiered refresh API.
/// </summary>
public sealed class InspectorTimelineRefresh
{
    /// <summary>
    /// Rebuild one timeline's node row (timeline-level refresh). Used for node edits rather than a
    /// pure node-visual update because inspector-edited values (repeat count, condition fields) can
    /// render on the row's block labels, which a single-node refresh would leave stale.
    /// </summary>
    public required Action<MacroTimeline> Row { get; init; }

    /// <summary>Re-skin the header cards' active/selected/meta state (visual-state refresh).</summary>
    public required Action HeaderStates { get; init; }

    /// <summary>Full editor rebuild — fallback only (e.g. when no timeline context is available).</summary>
    public required Action Full { get; init; }
}
