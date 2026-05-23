namespace KeySpammer.Domain;

public enum MacroStepType
{
    KeyDown,
    KeyUp,
    Delay,
    Text
}

public sealed class MacroStep
{
    public MacroStepType Type { get; set; }

    public string KeyName { get; set; } = "";
    public int VirtualKey { get; set; }

    public int DelayMs { get; set; }

    public string Text { get; set; } = "";

    public bool IsRecordedDelay { get; set; }

    public bool IsSyntheticDisplayStep { get; set; }

    public List<MacroStep> SourceSteps { get; set; } = new();
}