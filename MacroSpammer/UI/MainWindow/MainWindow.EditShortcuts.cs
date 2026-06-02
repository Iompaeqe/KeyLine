using System.Windows;
using System.Windows.Input;
using MacroSpammer.Domain;
using MacroSpammer.Services.Input;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private const int MaxUndoSnapshots = 30;

    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _undoStacks = new();
    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _redoStacks = new();
    private EditClipboard? _editClipboard;

    private bool TryHandleEditingShortcut(KeyEventArgs e)
    {
        if (_shortcutController == null)
            return false;

        var isBlocked = _runners.Values.Any(runner => runner.IsRunning);

        if (!_shortcutController.TryGetEditingCommand(
                e,
                isBlocked,
                SettingsModalOverlay.Visibility == Visibility.Visible,
                out var command))
        {
            return false;
        }

        switch (command)
        {
            case AppShortcutCommand.Undo:
                UndoActiveWorkspace();
                break;

            case AppShortcutCommand.Redo:
                RedoActiveWorkspace();
                break;

            case AppShortcutCommand.SelectAll:
                SelectAllNodesInActiveTimeline();
                break;

            case AppShortcutCommand.Copy:
                CopySelection();
                break;

            case AppShortcutCommand.Paste:
                PasteSelection();
                break;

            case AppShortcutCommand.Duplicate:
                DuplicateSelection();
                break;

            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    private void SaveUndoSnapshot()
    {
        CaptureActiveWorkspaceState();
        var stack = GetUndoStack(_activeWorkspace);
        stack.Push(MacroCloneService.CloneWorkspace(_activeWorkspace));

        while (stack.Count > MaxUndoSnapshots)
            TrimOldest(stack);

        GetRedoStack(_activeWorkspace).Clear();
    }

    private void UndoActiveWorkspace()
    {
        var undoStack = GetUndoStack(_activeWorkspace);
        if (undoStack.Count == 0)
            return;

        GetRedoStack(_activeWorkspace).Push(MacroCloneService.CloneWorkspace(_activeWorkspace));
        RestoreWorkspaceSnapshot(_activeWorkspace, undoStack.Pop());
    }

    private void RedoActiveWorkspace()
    {
        var redoStack = GetRedoStack(_activeWorkspace);
        if (redoStack.Count == 0)
            return;

        GetUndoStack(_activeWorkspace).Push(MacroCloneService.CloneWorkspace(_activeWorkspace));
        RestoreWorkspaceSnapshot(_activeWorkspace, redoStack.Pop());
    }

    private Stack<MacroWorkspace> GetUndoStack(MacroWorkspace workspace)
    {
        if (!_undoStacks.TryGetValue(workspace, out var stack))
        {
            stack = new Stack<MacroWorkspace>();
            _undoStacks[workspace] = stack;
        }

        return stack;
    }

    private Stack<MacroWorkspace> GetRedoStack(MacroWorkspace workspace)
    {
        if (!_redoStacks.TryGetValue(workspace, out var stack))
        {
            stack = new Stack<MacroWorkspace>();
            _redoStacks[workspace] = stack;
        }

        return stack;
    }

    private static void TrimOldest<T>(Stack<T> stack)
    {
        var items = stack.Reverse().Skip(1).Reverse().ToArray();
        stack.Clear();
        foreach (var item in items)
            stack.Push(item);
    }

    private void RestoreWorkspaceSnapshot(MacroWorkspace target, MacroWorkspace snapshot)
    {
        target.Name = snapshot.Name;
        target.Document = MacroCloneService.CloneDocument(snapshot.Document);
        target.LoopCount = snapshot.LoopCount;
        target.TimerMs = snapshot.TimerMs;
        target.BaseDelayMs = snapshot.BaseDelayMs;
        target.ShortcutKeys = snapshot.ShortcutKeys;
        target.TargetWindowSearchName = snapshot.TargetWindowSearchName;
        target.TargetWindowHandle = snapshot.TargetWindowHandle;
        target.TargetWindowTitle = snapshot.TargetWindowTitle;
        target.TargetChildWindowHandle = snapshot.TargetChildWindowHandle;
        target.TargetChildWindowTitle = snapshot.TargetChildWindowTitle;

        _document = target.Document;
        _selection.Clear();
        ActivateWorkspace(_activeWorkspaceIndex, false);
        ScheduleSaveState();
    }

    private void CopySelection()
    {
        if (_selection.HasNodeSelection && _selection.SelectedTimeline != null)
        {
            var rawSteps = GetSelectedRawSteps(_selection.SelectedTimeline);
            _editClipboard = EditClipboard.ForSteps(rawSteps.Select(MacroCloneService.CloneStep).ToList());
            return;
        }

        if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
        {
            _editClipboard = EditClipboard.ForTimelines(new[]
                { MacroCloneService.CloneTimeline(_selection.SelectedTimeline) });
            return;
        }

        _editClipboard = EditClipboard.ForWorkspaces(new[] { MacroCloneService.CloneWorkspace(_activeWorkspace) });
    }

    private void SelectStepFromPointer(MacroTimeline timeline, MacroNode node)
    {
        SelectTimeline(timeline);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ToggleStepSelection(timeline, node);
            RefreshInspector();
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selection.AnchorNode != null &&
            ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            if (SelectStepRange(timeline, node))
            {
                RefreshInspector();
                return;
            }
        }

        _selection.SelectNode(timeline, node);
        RefreshInspector();
    }

    private void SelectAllNodesInActiveTimeline()
    {
        var timeline = _document.ActiveTimeline;
        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        if (visibleSteps.Count == 0)
        {
            SelectTimeline(timeline);
            RefreshTimeline();
            return;
        }

        SelectTimeline(timeline);
        _selection.SelectNodes(timeline, visibleSteps, visibleSteps[^1]);
        RefreshInspector();
        RefreshTimeline();
    }

    private bool SelectStepRange(MacroTimeline timeline, MacroNode node)
    {
        if (_selection.AnchorNode == null)
            return false;

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Nodes.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var start = visibleSteps.IndexOf(_selection.AnchorNode);
        var end = visibleSteps.IndexOf(node);
        if (start < 0 || end < 0)
            return false;

        if (start > end)
            (start, end) = (end, start);

        var mergedSelection = _selection.SelectedNodes
            .Concat(visibleSteps.Skip(start).Take(end - start + 1))
            .Distinct()
            .ToList();

        _selection.SelectNodes(timeline, mergedSelection, node);
        return true;
    }

    private void ToggleStepSelection(MacroTimeline timeline, MacroNode node)
    {
        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            _selection.SelectNode(timeline, node);
            return;
        }

        var selectedSteps = _selection.SelectedNodes.ToList();
        var existing = selectedSteps.FirstOrDefault(selectedStep => IsSameSelectedStep(node, selectedStep));
        if (existing != null)
            selectedSteps.Remove(existing);
        else
            selectedSteps.Add(node);

        if (selectedSteps.Count == 0)
            _selection.Clear();
        else
            _selection.SelectNodes(timeline, selectedSteps, node);
    }

    private void PasteSelection()
    {
        if (_editClipboard == null)
            return;

        SaveUndoSnapshot();

        switch (_editClipboard.Kind)
        {
            case EditClipboardKind.Nodes:
                PasteSteps(_editClipboard.Nodes);
                break;
            case EditClipboardKind.Timelines:
                PasteTimelines(_editClipboard.Timelines);
                break;
            case EditClipboardKind.Workspaces:
                PasteWorkspaces(_editClipboard.Workspaces);
                break;
        }

        RefreshTimeline();
        RefreshMacroTabs();
        ScheduleSaveState();
    }

    private void DuplicateSelection()
    {
        if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
        {
            DuplicateTimeline(_selection.SelectedTimeline);
            return;
        }

        if (!_selection.HasNodeSelection)
        {
            DuplicateWorkspace(_activeWorkspaceIndex);
            return;
        }

        CopySelection();
        PasteSelection();
    }

    private void PasteSteps(IReadOnlyList<MacroNode> steps)
    {
        if (steps.Count == 0)
            return;

        var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
        var insertIndex = timeline.Nodes.Count;

        if (_selection.HasNodeSelection)
        {
            var selectedRawSteps = GetSelectedRawSteps(timeline);
            if (selectedRawSteps.Count > 0)
                insertIndex = timeline.Nodes.IndexOf(selectedRawSteps[^1]) + 1;
        }

        var clones = steps.Select(MacroCloneService.CloneStep).ToList();
        for (var i = 0; i < clones.Count; i++)
            timeline.Nodes.Insert(insertIndex + i, clones[i]);

        _selection.SelectNodes(timeline, clones);
        MergeAdjacentDelayNodesIfEnabled(timeline);
        SelectTimeline(timeline);
    }

    private void PasteTimelines(IReadOnlyList<MacroTimeline> timelines)
    {
        var insertIndex = _document.Timelines.Count;
        if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
        {
            var selectedIndex = _document.Timelines.IndexOf(_selection.SelectedTimeline);
            if (selectedIndex >= 0)
                insertIndex = selectedIndex + 1;
        }

        MacroTimeline? lastClone = null;
        foreach (var timeline in timelines)
        {
            lastClone = MacroCloneService.CloneTimeline(timeline);
            _document.Timelines.Insert(insertIndex++, lastClone);
        }

        _document.EnsureTimeline();
        if (lastClone != null)
        {
            _document.SelectTimeline(lastClone);
            _selection.SelectTimeline(lastClone);
        }
    }

    private void PasteWorkspaces(IReadOnlyList<MacroWorkspace> workspaces)
    {
        var insertIndex = Math.Clamp(_activeWorkspaceIndex + 1, 0, _workspaces.Count);
        foreach (var workspace in workspaces)
        {
            var clone = MacroCloneService.CloneWorkspace(workspace);
            clone.Name = WorkspaceNameService.GetUniqueName(_workspaces, clone.Name);
            _workspaces.Insert(insertIndex++, clone);
        }

        ActivateWorkspace(insertIndex - 1);
    }

    private void DuplicateTimeline(MacroTimeline source)
    {
        var sourceIndex = _document.Timelines.IndexOf(source);
        if (sourceIndex < 0)
            return;

        SaveUndoSnapshot();

        var clone = MacroCloneService.CloneTimeline(source);
        _document.Timelines.Insert(sourceIndex + 1, clone);
        _document.EnsureTimeline();
        _document.SelectTimeline(clone);
        _selection.SelectTimeline(clone);

        RefreshTimeline();
        ScheduleSaveState();
    }

    private void DuplicateWorkspace(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _workspaces.Count)
            return;

        CaptureActiveWorkspaceState();

        var source = _workspaces[sourceIndex];
        var clone = MacroCloneService.CloneWorkspace(source);
        clone.Name = WorkspaceNameService.GetUniqueDuplicateName(_workspaces, source.Name);

        _workspaces.Insert(sourceIndex + 1, clone);
        ActivateWorkspace(sourceIndex + 1);
    }

    private List<MacroNode> GetSelectedRawSteps(MacroTimeline timeline)
    {
        var selectedSteps = _selection.SelectedNodes.Count > 0
            ? _selection.SelectedNodes
            : _selection.SelectedNode != null
                ? new List<MacroNode> { _selection.SelectedNode }
                : new List<MacroNode>();

        return selectedSteps
            .SelectMany(step => TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, step))
            .Distinct()
            .OrderBy(step => timeline.Nodes.IndexOf(step))
            .ToList();
    }

    private sealed class EditClipboard
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

    private enum EditClipboardKind
    {
        Nodes,
        Timelines,
        Workspaces
    }
}
