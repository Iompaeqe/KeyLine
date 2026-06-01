using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using MacroSpammer.Domain;
using MacroSpammer.Services.Input;
using MacroSpammer.Services.Macro;

namespace MacroSpammer;

public partial class MainWindow
{
    private const int MaxUndoSnapshots = 30;

    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _undoStacks = new();
    private readonly Dictionary<MacroWorkspace, Stack<MacroWorkspace>> _redoStacks = new();
    private EditClipboard? _editClipboard;

    private bool TryHandleEditingShortcut(KeyEventArgs e)
    {
        if (_runners.Values.Any(runner => runner.IsRunning) ||
            _isCapturingShortcut ||
            SettingsModalOverlay.Visibility == Visibility.Visible ||
            IsTextEditingShortcutSource(e.OriginalSource as DependencyObject))
        {
            return false;
        }

        var pressedKeys = GetCurrentShortcutKeys(e);

        if (MatchesLocalShortcut(pressedKeys, _settings.UndoShortcut))
        {
            UndoActiveWorkspace();
            e.Handled = true;
            return true;
        }

        if (MatchesLocalShortcut(pressedKeys, _settings.RedoShortcut))
        {
            RedoActiveWorkspace();
            e.Handled = true;
            return true;
        }

        if (MatchesLocalShortcut(pressedKeys, _settings.SelectAllShortcut))
        {
            SelectAllNodesInActiveTimeline();
            e.Handled = true;
            return true;
        }

        if (MatchesLocalShortcut(pressedKeys, _settings.CopyShortcut))
        {
            CopySelection();
            e.Handled = true;
            return true;
        }

        if (MatchesLocalShortcut(pressedKeys, _settings.PasteShortcut))
        {
            PasteSelection();
            e.Handled = true;
            return true;
        }

        if (MatchesLocalShortcut(pressedKeys, _settings.DuplicateShortcut))
        {
            DuplicateSelection();
            e.Handled = true;
            return true;
        }

        return false;
    }

    private static HashSet<int> GetCurrentShortcutKeys(KeyEventArgs e)
    {
        var keys = new HashSet<int>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            keys.Add(MacroSpammer.Interop.NativeMethods.VK_CONTROL);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            keys.Add(MacroSpammer.Interop.NativeMethods.VK_SHIFT);
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            keys.Add(MacroSpammer.Interop.NativeMethods.VK_MENU);

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
        if (virtualKey > 0)
            keys.Add(virtualKey);

        return keys;
    }

    private static bool MatchesLocalShortcut(IReadOnlySet<int> pressedKeys, string shortcut) =>
        ShortcutGesture.Matches(pressedKeys, ShortcutGesture.Parse(shortcut));

    private static bool IsTextEditingShortcutSource(DependencyObject? source)
    {
        for (var current = source; current != null; current = GetShortcutSourceParent(current))
        {
            if (current is TextBoxBase or PasswordBox or ComboBox)
                return true;
        }

        return false;
    }

    private static DependencyObject? GetShortcutSourceParent(DependencyObject current)
    {
        return current switch
        {
            FrameworkContentElement contentElement => contentElement.Parent,
            FrameworkElement frameworkElement => frameworkElement.Parent,
            Visual or Visual3D => VisualTreeHelper.GetParent(current),
            _ => null
        };
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
        if (_selection.HasStepSelection && _selection.SelectedTimeline != null)
        {
            var rawSteps = GetSelectedRawSteps(_selection.SelectedTimeline);
            _editClipboard = EditClipboard.ForSteps(rawSteps.Select(MacroCloneService.CloneStep).ToList());
            return;
        }

        if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
        {
            _editClipboard = EditClipboard.ForTimelines(new[] { MacroCloneService.CloneTimeline(_selection.SelectedTimeline) });
            return;
        }

        _editClipboard = EditClipboard.ForWorkspaces(new[] { MacroCloneService.CloneWorkspace(_activeWorkspace) });
    }

    private void SelectStepFromPointer(MacroTimeline timeline, MacroStep step)
    {
        SelectTimeline(timeline);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ToggleStepSelection(timeline, step);
            RefreshInspector();
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _selection.AnchorStep != null &&
            ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            if (SelectStepRange(timeline, step))
            {
                RefreshInspector();
                return;
            }
        }

        _selection.SelectStep(timeline, step);
        RefreshInspector();
    }

    private void SelectAllNodesInActiveTimeline()
    {
        var timeline = _document.ActiveTimeline;
        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Steps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        if (visibleSteps.Count == 0)
        {
            SelectTimeline(timeline);
            RefreshTimeline();
            return;
        }

        SelectTimeline(timeline);
        _selection.SelectSteps(timeline, visibleSteps, visibleSteps[^1]);
        RefreshInspector();
        RefreshTimeline();
    }

    private bool SelectStepRange(MacroTimeline timeline, MacroStep step)
    {
        if (_selection.AnchorStep == null)
            return false;

        var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
            timeline.Steps.ToList(),
            timeline.UseStandardDelay,
            timeline.ShowKeyUpDown);

        var start = visibleSteps.IndexOf(_selection.AnchorStep);
        var end = visibleSteps.IndexOf(step);
        if (start < 0 || end < 0)
            return false;

        if (start > end)
            (start, end) = (end, start);

        var mergedSelection = _selection.SelectedSteps
            .Concat(visibleSteps.Skip(start).Take(end - start + 1))
            .Distinct()
            .ToList();

        _selection.SelectSteps(timeline, mergedSelection, step);
        return true;
    }

    private void ToggleStepSelection(MacroTimeline timeline, MacroStep step)
    {
        if (!ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            _selection.SelectStep(timeline, step);
            return;
        }

        var selectedSteps = _selection.SelectedSteps.ToList();
        var existing = selectedSteps.FirstOrDefault(selectedStep => IsSameSelectedStep(step, selectedStep));
        if (existing != null)
            selectedSteps.Remove(existing);
        else
            selectedSteps.Add(step);

        if (selectedSteps.Count == 0)
            _selection.Clear();
        else
            _selection.SelectSteps(timeline, selectedSteps, step);
    }

    private void PasteSelection()
    {
        if (_editClipboard == null)
            return;

        SaveUndoSnapshot();

        switch (_editClipboard.Kind)
        {
            case EditClipboardKind.Steps:
                PasteSteps(_editClipboard.Steps);
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

        if (!_selection.HasStepSelection)
        {
            DuplicateWorkspace(_activeWorkspaceIndex);
            return;
        }

        CopySelection();
        PasteSelection();
    }

    private void PasteSteps(IReadOnlyList<MacroStep> steps)
    {
        if (steps.Count == 0)
            return;

        var timeline = _selection.SelectedTimeline ?? _document.ActiveTimeline;
        var insertIndex = timeline.Steps.Count;

        if (_selection.HasStepSelection)
        {
            var selectedRawSteps = GetSelectedRawSteps(timeline);
            if (selectedRawSteps.Count > 0)
                insertIndex = timeline.Steps.IndexOf(selectedRawSteps[^1]) + 1;
        }

        var clones = steps.Select(MacroCloneService.CloneStep).ToList();
        for (var i = 0; i < clones.Count; i++)
            timeline.Steps.Insert(insertIndex + i, clones[i]);

        _selection.SelectSteps(timeline, clones);
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
            clone.Name = GetUniqueWorkspaceName(clone.Name);
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
        clone.Name = GetUniqueDuplicateWorkspaceName(source.Name);

        _workspaces.Insert(sourceIndex + 1, clone);
        ActivateWorkspace(sourceIndex + 1);
    }

    private string GetUniqueDuplicateWorkspaceName(string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Macro" : sourceName.Trim();
        var preferredName = $"{baseName} - dub";

        if (_workspaces.All(workspace => !string.Equals(workspace.Name, preferredName, StringComparison.OrdinalIgnoreCase)))
            return preferredName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{preferredName} {i}";
            if (_workspaces.All(workspace => !string.Equals(workspace.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    private List<MacroStep> GetSelectedRawSteps(MacroTimeline timeline)
    {
        var selectedSteps = _selection.SelectedSteps.Count > 0
            ? _selection.SelectedSteps
            : _selection.SelectedStep != null
                ? new List<MacroStep> { _selection.SelectedStep }
                : new List<MacroStep>();

        return selectedSteps
            .SelectMany(step => GetRawStepsForDisplayStep(timeline, step))
            .Distinct()
            .OrderBy(step => timeline.Steps.IndexOf(step))
            .ToList();
    }

    private sealed class EditClipboard
    {
        public EditClipboardKind Kind { get; private init; }
        public List<MacroStep> Steps { get; private init; } = new();
        public List<MacroTimeline> Timelines { get; private init; } = new();
        public List<MacroWorkspace> Workspaces { get; private init; } = new();

        public static EditClipboard ForSteps(List<MacroStep> steps) =>
            new() { Kind = EditClipboardKind.Steps, Steps = steps };

        public static EditClipboard ForTimelines(IEnumerable<MacroTimeline> timelines) =>
            new() { Kind = EditClipboardKind.Timelines, Timelines = timelines.ToList() };

        public static EditClipboard ForWorkspaces(IEnumerable<MacroWorkspace> workspaces) =>
            new() { Kind = EditClipboardKind.Workspaces, Workspaces = workspaces.ToList() };
    }

    private enum EditClipboardKind
    {
        Steps,
        Timelines,
        Workspaces
    }
}
