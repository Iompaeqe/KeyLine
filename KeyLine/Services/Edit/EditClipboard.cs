using KeyLine.Domain;

namespace KeyLine.Services.Edit;

public sealed class EditClipboard
{
    public EditClipboardKind Kind { get; private init; }
    public List<MacroNode> Nodes { get; private init; } = new();
    public List<MacroTimeline> Timelines { get; private init; } = new();
    public List<MacroWorkspace> Workspaces { get; private init; } = new();

    public static EditClipboard ForSteps(List<MacroNode> nodes) =>
        new() { Kind = EditClipboardKind.Nodes, Nodes = nodes };

    public static EditClipboard ForTimelines(IEnumerable<MacroTimeline> timelines) =>
        new() { Kind = EditClipboardKind.Timelines, Timelines = timelines.ToList() };

    public static EditClipboard ForWorkspaces(IEnumerable<MacroWorkspace> workspaces) =>
        new() { Kind = EditClipboardKind.Workspaces, Workspaces = workspaces.ToList() };
}
