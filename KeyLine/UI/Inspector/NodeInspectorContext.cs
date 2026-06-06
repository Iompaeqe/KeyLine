using KeyLine.Domain;

namespace KeyLine.UI.Inspector;

public sealed class NodeInspectorContext
{
    public NodeInspectorContext(
        Func<MacroWorkspace> getActiveWorkspace,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<bool> canEdit,
        Func<bool> isRefreshing,
        Action saveUndoSnapshot,
        Action<Action> commitNodeChange,
        Action<Action> commitNodeValueChange,
        Action refreshInspector,
        Func<MacroNode, Task> pickMouseCoordinatesForNodeAsync,
        Func<MacroNode, Task> pickConditionPixelAsync)
    {
        GetActiveWorkspace = getActiveWorkspace;
        GetActiveProfileWorkspaces = getActiveProfileWorkspaces;
        CanEdit = canEdit;
        IsRefreshing = isRefreshing;
        SaveUndoSnapshot = saveUndoSnapshot;
        CommitNodeChange = commitNodeChange;
        CommitNodeValueChange = commitNodeValueChange;
        RefreshInspector = refreshInspector;
        PickMouseCoordinatesForNodeAsync = pickMouseCoordinatesForNodeAsync;
        PickConditionPixelAsync = pickConditionPixelAsync;
    }

    public Func<MacroWorkspace> GetActiveWorkspace { get; }
    public Func<IReadOnlyList<MacroWorkspace>> GetActiveProfileWorkspaces { get; }
    public Func<bool> CanEdit { get; }
    public Func<bool> IsRefreshing { get; }

    public Action SaveUndoSnapshot { get; }
    public Action<Action> CommitNodeChange { get; }
    public Action<Action> CommitNodeValueChange { get; }
    public Action RefreshInspector { get; }

    public Func<MacroNode, Task> PickMouseCoordinatesForNodeAsync { get; }
    public Func<MacroNode, Task> PickConditionPixelAsync { get; }

    public bool CanEditOption(bool optionEnabled)
    {
        return CanEdit() && optionEnabled;
    }
}