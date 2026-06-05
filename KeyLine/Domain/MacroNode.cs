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
    MouseScrollDown
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
