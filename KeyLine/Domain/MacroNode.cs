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
    RepeatEnd
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

    public bool IsRecordedDelay { get; set; }

    public bool IsSyntheticDisplayNode { get; set; }

    public List<MacroNode> SourceNodes { get; set; } = new();

    public string RepeatBlockId { get; set; } = "";

    public int RepeatCount { get; set; } = 2;
}
