namespace KeyLine.Domain;

public enum MacroLoopMode
{
    Async,
    Sync,
    Cycle,
    Chain
}

public enum ShortcutTriggerBehavior
{
    PassThrough,
    RemapConsume
}

public sealed class MacroWorkspace
{
    public string ProfileId { get; set; } = MacroProfile.NoProfileId;

    public string Name { get; set; } = "Macro 1";

    public MacroDocument Document { get; set; } = new();

    public int LoopCount { get; set; }

    public int TimerMs { get; set; }

    public int BaseDelayMs { get; set; } = 50;

    public MacroLoopMode LoopMode { get; set; } = MacroLoopMode.Async;

    public string ShortcutKeys { get; set; } = "";

    public bool ShortcutsEnabled { get; set; }

    public ShortcutTriggerBehavior ShortcutTriggerBehavior { get; set; } = ShortcutTriggerBehavior.PassThrough;

    public string TargetWindowSearchName { get; set; } = "";

    public long TargetWindowHandle { get; set; }

    public string TargetWindowTitle { get; set; } = "";

    public long TargetChildWindowHandle { get; set; }

    public string TargetChildWindowTitle { get; set; } = "";

    public string ErrorMessage { get; set; } = "";
}

