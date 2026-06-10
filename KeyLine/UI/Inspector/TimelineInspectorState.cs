namespace KeyLine.UI.Inspector;

public sealed record TimelineInspectorState(
    string TimelineName,
    bool IsNameEditing,
    bool IsCollapsed,
    bool IsEditingEnabled,
    int LoopCount,
    int LoopDelayMs,
    bool UseTextInputMode,
    bool UseStandardDelay,
    int StandardDelayMs,
    bool ShowKeyUpDown,
    int CooldownMs = 0,
    bool ShowCooldown = false,
    bool IsNameReadOnly = false,
    // Batch mode (multiple timelines selected). When SelectedCount > 1 the inspector edits every
    // selected timeline at once and shows a "Mixed" indicator for fields whose values differ.
    int SelectedCount = 1,
    bool LoopCountMixed = false,
    bool LoopDelayMixed = false,
    bool CooldownMixed = false,
    bool StandardDelayMixed = false,
    bool UseStandardDelayMixed = false,
    bool ShowKeyUpDownMixed = false,
    bool UseTextInputModeMixed = false)
{
    public bool IsBatch => SelectedCount > 1;
}
