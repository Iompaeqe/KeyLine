namespace MacroSpammer.UI;

public static class TooltipNotes
{
    public const string ToggleInspector = "Toggle Inspector";
    public const string OpenSettings = "Settings";
    public const string TargetTopLevelWindow = "Target top-level window used for playback.";
    public const string TargetChildWindow = "Specific part of the target window used for playback.";
    public const string TargetWindowSearchName = "Window title text used to automatically find a target when this macro has no selected target.";
    public const string MacroShortcutCapture = "Left-click to set. Right-click to clear.";
    public const string MacroShortcutToggle = "Enable or disable the shortcut for this macro.";
    public const string MacroLoopType = "Choose how timelines restart when looping.";
    public const string MacroLoopTypeAsync = "Async: Each timeline restarts as soon as its own loop finishes.";
    public const string MacroLoopTypeSync = "Sync: Timelines wait for each other before starting the next loop.";
    public const string AddTimeline = "Add a new timeline to this macro.";
    public const string MacroTimer = "Exact duration for the macro to run. 0 means infinite.";
    public const string ClearSelection = "Clear the selected timeline or current selection.";
    public const string StartPlayback = "Start playback for the current macro.";
    public const string PauseResumePlayback = "Pause or resume playback.";
    public const string StopPlayback = "Stop playback.";

    public const string MacroTabRunning = "This macro is currently running.";
    public const string MacroTabSwitchRenameDelete = "Click to switch macro. Double-click to rename. Right-click to delete.";
    public const string RenameMacro = "Rename macro";

    public const string TimelineSectionToggle = "Show or hide timeline settings.";
    public const string NodeSectionToggle = "Show or hide node settings.";
    public const string TimelineName = "Timeline name. Click the pencil to rename it.";
    public const string RenameTimeline = "Rename timeline";
    public const string EditTimelineName = "Edit the timeline name.";
    public const string TimelineLoops = "How many times this timeline runs. 0 means infinite.";
    public const string TimelineLoopDelay = "Delay before this timeline starts its next loop.";
    public const string TimelineLoopDelayEdit = "Delay before this timeline starts its next loop.";
    public const string TimelineInputType = "Choose whether keyboard nodes send key messages or text input.";
    public const string TimelineStandardDelay = "Use one shared delay between nodes in this timeline.";
    public const string TimelineStandardDelayValue = "Shared delay inserted between nodes.";
    public const string TimelineStandardDelayEdit = "Shared delay inserted between nodes.";
    public const string TimelineShowKeyUpDown = "Show key down and key up nodes separately.";

    public const string DelayStepValue = "Delay before the next node.";
    public const string RandomDelayMinimum = "Minimum random delay.";
    public const string RandomDelayMaximum = "Maximum random delay.";
    public const string TextNodeValue = "Text sent by this node.";
    public const string TextNodeEdit = "Text sent by this node.";
    public const string PickMouseCoordinates = "Pick the mouse coordinates in the target window.";

    public const string StandardDelayRequiresEnable = "Enable standard delay to edit this setting.";
}
