namespace KeySpammer.Domain;

public sealed class MacroWorkspace
{
    public string Name { get; set; } = "Macro 1";

    public MacroDocument Document { get; set; } = new();

    public int LoopCount { get; set; }

    public string TargetWindowTitle { get; set; } = "";

    public string TargetChildWindowTitle { get; set; } = "";
}
