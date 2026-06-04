namespace KeyLine.UI;

public static class TooltipNotes
{
    public const string ToggleInspector = "Toggle Inspector";
    public const string OpenSettings = "Settings";
    public const string TargetTopLevelWindow = "Target top-level window used for playback.";
    public const string TargetChildWindow = "Specific part of the target window used for playback.";
    public const string TargetWindowSearchName = "Window title text used to automatically find a target when this macro has no selected target.";
    public const string MacroShortcutCapture = "Left-click to set. Right-click to clear.";
    public const string MacroShortcutToggle = "Enable or disable the shortcut for this macro.";
    public const string MacroLoopMode = "Choose how multiple timelines are scheduled.";
    public const string MacroLoopModeAsync = "Async: all runnable timelines start together. Each timeline repeats independently until its own loop count is reached.";
    public const string MacroLoopModeSync = "Sync: all runnable timelines start each pass together. Timelines with fewer loops drop out after they reach their count.";
    public const string MacroLoopModeCycle = "Cycle: timelines run top to bottom, one at a time. Each pass skips timelines that already reached their loop count; 0 loops means that timeline keeps participating forever.";
    public const string MacroLoopModeChain = "Chain: each timeline runs until its loop count expires, then playback moves to the next timeline.";
    public const string AddTimeline = "Add a new timeline to this macro.";
    public const string MacroTimer = "Exact duration for the macro to run. 0 means infinite.";
    public const string ClearSelection = "Clear the selected timeline or current selection.";
    public const string StartPlayback = "Start playback for the current macro.";
    public const string PauseResumePlayback = "Pause or resume playback.";
    public const string StopPlayback = "Stop playback.";

    public const string MacroTabRunning = "This macro is currently running.";
    public const string MacroTabSwitchRenameDelete = "Click to switch macro. Middle-click to delete. Right-click for options.";
    public const string RenameMacro = "Rename macro";

    public const string TimelineSectionToggle = "Show or hide timeline settings.";
    public const string NodeSectionToggle = "Show or hide node settings.";
    public const string TimelineName = "Timeline name. Click the pencil to rename it.";
    public const string RenameTimeline = "Rename timeline";
    public const string EditTimelineName = "Edit the timeline name.";
    public const string TimelineLoops = "How many times this timeline runs. 0 means infinite.";
    public const string TimelineLoopDelay = "Delay after this timeline completes a pass. In async/sync modes this is the delay before its next loop; in cycle and chain modes this separates this timeline from the next eligible timeline.";
    public const string TimelineLoopDelayEdit = "Delay after this timeline completes a pass. In cycle and chain modes this separates this timeline from the next eligible timeline.";
    public const string TimelineInputType = "Choose whether keyboard nodes send key messages or text input.";
    public const string TimelineStandardDelay = "Use one shared delay between nodes in this timeline.";
    public const string TimelineStandardDelayValue = "Shared delay inserted between nodes.";
    public const string TimelineStandardDelayEdit = "Shared delay inserted between nodes.";
    public const string TimelineShowKeyUpDown = "Show key down and key up nodes separately.";

    public const string DelayNodeValue = "Delay before the next node.";
    public const string RandomDelayMinimum = "Minimum random delay.";
    public const string RandomDelayMaximum = "Maximum random delay.";
    public const string TextNodeValue = "Text sent by this node.";
    public const string TextNodeEdit = "Text sent by this node.";
    public const string PickMouseCoordinates = "Pick the mouse coordinates in the target window.";
    public const string RepeatCount = "How many times the nodes inside this Repeat block run.";
    public const string ConditionType = "Choose the simple condition that gates the nodes inside this block.";
    public const string ConditionKeyState = "The block runs only while this key or mouse button is held.";
    public const string ConditionPixelPosition = "Target-window pixel position to compare before running the block.";
    public const string ConditionPixelColor = "Expected pixel color channel value.";
    public const string ConditionPixelTolerance = "Allowed color difference per RGB channel.";
    public const string ConditionPixelPick = "Pick a target-window pixel and capture its current color.";
    public const string ConditionChance = "Chance that the block runs when execution reaches it.";
    public const string ConditionLoopContext = "Timeline or repeat iteration rule used by this condition.";
    public const string ConditionLoopInterval = "Run on every Nth loop or repeat iteration.";

    public const string StandardDelayRequiresEnable = "Enable standard delay to edit this setting.";
}

