namespace KeyLine.Domain;

public enum MacroNodeType
{
    KeyDown,
    KeyUp,
    Delay,
    RandomDelay,
    Text,
    MouseClick,
    MouseDown,
    MouseUp,
    CursorMove,
    BackgroundMouseDown,
    BackgroundMouseUp,
    BackgroundMouseClick,
    RepeatStart,
    RepeatEnd,
    ConditionStart,
    ConditionEnd,
    MouseScrollUp,
    MouseScrollDown,
    MouseScrollLeft,
    MouseScrollRight,
    SystemOpenLaunch,
    SystemVolumeControl,
    SystemWaitUntilWindowOpens,
    SystemSelectTargetWindow,
    SystemFocusWindow
}

public enum SystemLaunchKind
{
    Application,
    File,
    Folder,
    Url
}

public enum SystemVolumeAction
{
    VolumeUp,
    VolumeDown,
    MuteToggle,
    Mute,
    Unmute,
    SetVolumePercent
}

public enum WindowReferenceType
{
    SelectedTarget,
    FocusedWindow,
    LastLaunchedWindow,
    LastFoundWindow,
    CustomTitle
}

public sealed class WindowReference
{
    public WindowReferenceType Type { get; set; } = WindowReferenceType.CustomTitle;

    public string CustomTitle { get; set; } = "";

    public static WindowReference Custom(string title) => new()
    {
        Type = WindowReferenceType.CustomTitle,
        CustomTitle = title
    };

    public WindowReference Clone() => new()
    {
        Type = Type,
        CustomTitle = CustomTitle
    };
}

public enum MacroConditionType
{
    KeyState,
    PixelColor,
    RandomChance,
    LoopContext
}

public enum MacroConditionLoopMode
{
    FirstLoop,
    LastLoop,
    EveryNLoops,
    FirstRepeat,
    LastRepeat,
    EveryNRepeats
}

public sealed class MacroNode
{
    public MacroNodeType Type { get; set; }

    public string KeyName { get; set; } = "";
    public int VirtualKey { get; set; }

    public int DelayMs { get; set; }

    public int RandomDelayMinMs { get; set; }

    public int RandomDelayMaxMs { get; set; }

    public string Text { get; set; } = "";

    public int MouseX { get; set; }

    public int MouseY { get; set; }

    public int MouseButton { get; set; } = 1;

    public int MouseWheelDelta { get; set; }

    public int MouseScrollAmount { get; set; } = 1;

    public SystemLaunchKind SystemLaunchKind { get; set; } = SystemLaunchKind.Application;

    public string SystemLaunchTarget { get; set; } = "";

    public SystemVolumeAction SystemVolumeAction { get; set; } = SystemVolumeAction.VolumeUp;

    public int SystemVolumePercent { get; set; } = 50;

    public string SystemWaitWindowTitle { get; set; } = "";

    public int SystemWaitPollIntervalMs { get; set; } = 250;

    public string SystemTargetWindowTitle { get; set; } = "";

    public WindowReference WindowReference { get; set; } = new();

    public WindowReference GetEffectiveWindowReference()
    {
        var reference = WindowReference?.Clone() ?? new WindowReference();
        if (!Enum.IsDefined(reference.Type))
            reference.Type = WindowReferenceType.CustomTitle;

        if (reference.Type != WindowReferenceType.CustomTitle)
            return reference;

        reference.CustomTitle = (reference.CustomTitle ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(reference.CustomTitle))
            return reference;

        var legacyTitle = Type == MacroNodeType.SystemSelectTargetWindow
            ? SystemTargetWindowTitle
            : SystemWaitWindowTitle;

        reference.CustomTitle = (legacyTitle ?? "").Trim();
        return reference;
    }

    public void NormalizeWindowReference()
    {
        WindowReference = GetEffectiveWindowReference();
        WindowReference.CustomTitle = (WindowReference.CustomTitle ?? "").Trim();

        if (WindowReference.Type != WindowReferenceType.CustomTitle)
            return;

        if (Type == MacroNodeType.SystemWaitUntilWindowOpens)
            SystemWaitWindowTitle = WindowReference.CustomTitle;
        else if (Type == MacroNodeType.SystemSelectTargetWindow)
            SystemTargetWindowTitle = WindowReference.CustomTitle;
    }

    public bool IsRecordedDelay { get; set; }

    public bool IsSyntheticDisplayNode { get; set; }

    public List<MacroNode> SourceNodes { get; set; } = new();

    public string RepeatBlockId { get; set; } = "";

    public int RepeatCount { get; set; } = 2;

    public string ConditionBlockId { get; set; } = "";

    public MacroConditionType ConditionType { get; set; } = MacroConditionType.KeyState;

    public string ConditionKeyName { get; set; } = "Shift";

    public int ConditionVirtualKey { get; set; } = 0x10;

    public string ConditionShortcutKeys { get; set; } = "";

    public int ConditionPixelX { get; set; }

    public int ConditionPixelY { get; set; }

    public int ConditionPixelRed { get; set; } = 255;

    public int ConditionPixelGreen { get; set; } = 255;

    public int ConditionPixelBlue { get; set; } = 255;

    public int ConditionPixelTolerance { get; set; }

    public int ConditionChancePercent { get; set; } = 30;

    public MacroConditionLoopMode ConditionLoopMode { get; set; } = MacroConditionLoopMode.FirstLoop;

    public int ConditionLoopInterval { get; set; } = 2;
}
