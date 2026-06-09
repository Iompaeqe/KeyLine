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
    bool ShowCooldown = false);