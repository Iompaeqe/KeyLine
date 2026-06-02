using KeyLine.Domain;
using KeyLine.Services.Macro;

namespace KeyLine.Services.Edit;

public sealed class WorkspaceHistoryController
{
    private const int MaxUndoSnapshots = 30;

    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _undoStacks = new();
    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _redoStacks = new();

    public void SaveSnapshot(MacroWorkspace workspace)
    {
        var stack = GetUndoStack(workspace);
        stack.Push(MacroCloneService.CloneWorkspace(workspace));

        while (stack.Count > MaxUndoSnapshots)
            TrimOldest(stack);

        GetRedoStack(workspace).Clear();
    }

    public bool TryUndo(MacroWorkspace workspace, out MacroWorkspace snapshot)
    {
        var undoStack = GetUndoStack(workspace);
        if (undoStack.Count == 0)
        {
            snapshot = null!;
            return false;
        }

        GetRedoStack(workspace).Push(MacroCloneService.CloneWorkspace(workspace));
        snapshot = undoStack.Pop();
        return true;
    }

    public bool TryRedo(MacroWorkspace workspace, out MacroWorkspace snapshot)
    {
        var redoStack = GetRedoStack(workspace);
        if (redoStack.Count == 0)
        {
            snapshot = null!;
            return false;
        }

        GetUndoStack(workspace).Push(MacroCloneService.CloneWorkspace(workspace));
        snapshot = redoStack.Pop();
        return true;
    }

    public void Clear(MacroWorkspace workspace)
    {
        _undoStacks.Remove(workspace);
        _redoStacks.Remove(workspace);
    }

    private Stack<MacroWorkspace> GetUndoStack(MacroWorkspace workspace)
    {
        if (_undoStacks.TryGetValue(workspace, out var stack))
            return stack;

        stack = new Stack<MacroWorkspace>();
        _undoStacks[workspace] = stack;
        return stack;
    }

    private Stack<MacroWorkspace> GetRedoStack(MacroWorkspace workspace)
    {
        if (_redoStacks.TryGetValue(workspace, out var stack))
            return stack;

        stack = new Stack<MacroWorkspace>();
        _redoStacks[workspace] = stack;
        return stack;
    }

    private static void TrimOldest<T>(Stack<T> stack)
    {
        var items = stack.Reverse().Skip(1).Reverse().ToArray();
        stack.Clear();

        foreach (var item in items)
            stack.Push(item);
    }
}
