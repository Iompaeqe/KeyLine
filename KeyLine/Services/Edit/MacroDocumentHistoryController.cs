using KeyLine.Domain;
using KeyLine.Services.Macro;

namespace KeyLine.Services.Edit;

public sealed class MacroDocumentHistoryController
{
    private const int MaxUndoSnapshots = 30;

    private readonly Dictionary<MacroWorkspace, Stack<MacroDocument>> _undoStacks = new();
    private readonly Dictionary<MacroWorkspace, Stack<MacroDocument>> _redoStacks = new();

    public void SaveSnapshot(MacroWorkspace workspace)
    {
        var stack = GetUndoStack(workspace);
        stack.Push(MacroCloneService.CloneDocument(workspace.Document));

        while (stack.Count > MaxUndoSnapshots)
            TrimOldest(stack);

        GetRedoStack(workspace).Clear();
    }

    public bool TryUndo(MacroWorkspace workspace, out MacroDocument snapshot)
    {
        var undoStack = GetUndoStack(workspace);
        if (undoStack.Count == 0)
        {
            snapshot = null!;
            return false;
        }

        GetRedoStack(workspace).Push(MacroCloneService.CloneDocument(workspace.Document));
        snapshot = undoStack.Pop();
        return true;
    }

    public bool TryRedo(MacroWorkspace workspace, out MacroDocument snapshot)
    {
        var redoStack = GetRedoStack(workspace);
        if (redoStack.Count == 0)
        {
            snapshot = null!;
            return false;
        }

        GetUndoStack(workspace).Push(MacroCloneService.CloneDocument(workspace.Document));
        snapshot = redoStack.Pop();
        return true;
    }

    public void Clear(MacroWorkspace workspace)
    {
        _undoStacks.Remove(workspace);
        _redoStacks.Remove(workspace);
    }

    private Stack<MacroDocument> GetUndoStack(MacroWorkspace workspace)
    {
        if (_undoStacks.TryGetValue(workspace, out var stack))
            return stack;

        stack = new Stack<MacroDocument>();
        _undoStacks[workspace] = stack;
        return stack;
    }

    private Stack<MacroDocument> GetRedoStack(MacroWorkspace workspace)
    {
        if (_redoStacks.TryGetValue(workspace, out var stack))
            return stack;

        stack = new Stack<MacroDocument>();
        _redoStacks[workspace] = stack;
        return stack;
    }

    private static void TrimOldest<T>(Stack<T> stack)
    {
        var items = stack.ToArray();
        stack.Clear();

        for (var i = items.Length - 2; i >= 0; i--)
        {
            var item = items[i];
            stack.Push(item);
        }
    }
}
