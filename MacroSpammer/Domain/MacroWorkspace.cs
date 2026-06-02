namespace MacroSpammer.Domain;

public enum MacroLoopType
{
    Async,
    Sync,
    Sequence
}

public sealed class MacroWorkspace
{
    public string Name { get; set; } = "Macro 1";

    public MacroDocument Document { get; set; } = new();

    public int LoopCount { get; set; }

    public int TimerMs { get; set; }

    public int BaseDelayMs { get; set; } = 50;

    public MacroLoopType LoopType { get; set; } = MacroLoopType.Async;

    public string ShortcutKeys { get; set; } = "";

    public string TargetWindowSearchName { get; set; } = "";

    public long TargetWindowHandle { get; set; }

    public string TargetWindowTitle { get; set; } = "";

    public long TargetChildWindowHandle { get; set; }

    public string TargetChildWindowTitle { get; set; } = "";

    public string ErrorMessage { get; set; } = "";
}
