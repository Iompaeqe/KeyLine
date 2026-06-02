using KeyLine.Domain;

namespace KeyLine.Services.Edit;

public sealed class EditClipboardController
{
    public EditClipboard? Current { get; private set; }

    public void SetSteps(List<MacroNode> nodes)
    {
        Current = EditClipboard.ForSteps(nodes);
    }

    public void SetTimelines(IEnumerable<MacroTimeline> timelines)
    {
        Current = EditClipboard.ForTimelines(timelines);
    }

    public void SetWorkspaces(IEnumerable<MacroWorkspace> workspaces)
    {
        Current = EditClipboard.ForWorkspaces(workspaces);
    }

    public void Clear()
    {
        Current = null;
    }
}
