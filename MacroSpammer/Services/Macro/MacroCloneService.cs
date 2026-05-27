using MacroSpammer.Domain;

namespace MacroSpammer.Services.Macro;

public static class MacroCloneService
{
    public static MacroWorkspace CloneWorkspace(MacroWorkspace source)
    {
        return new MacroWorkspace
        {
            Name = source.Name,
            Document = CloneDocument(source.Document),
            LoopCount = source.LoopCount,
            TimerMs = source.TimerMs,
            BaseDelayMs = source.BaseDelayMs,
            ShortcutKeys = source.ShortcutKeys,
            TargetWindowHandle = 0,
            TargetWindowTitle = "",
            TargetChildWindowHandle = 0,
            TargetChildWindowTitle = ""
        };
    }

    public static MacroDocument CloneDocument(MacroDocument source)
    {
        var clone = new MacroDocument();
        clone.Timelines.Clear();

        foreach (var timeline in source.Timelines)
            clone.Timelines.Add(CloneTimeline(timeline));

        clone.EnsureTimeline();
        clone.SelectTimeline(Math.Clamp(source.ActiveTimelineIndex, 0, clone.Timelines.Count - 1));
        return clone;
    }

    public static MacroTimeline CloneTimeline(MacroTimeline source)
    {
        var clone = new MacroTimeline
        {
            Name = source.Name,
            UseStandardDelay = source.UseStandardDelay,
            StandardDelayMs = source.StandardDelayMs,
            ShowKeyUpDown = source.ShowKeyUpDown,
            UseTextInputMode = source.UseTextInputMode
        };

        foreach (var step in source.Steps.Where(step => !step.IsSyntheticDisplayStep))
            clone.Steps.Add(CloneStep(step));

        return clone;
    }

    public static MacroStep CloneStep(MacroStep source)
    {
        return new MacroStep
        {
            Type = source.Type,
            KeyName = source.KeyName,
            VirtualKey = source.VirtualKey,
            DelayMs = source.DelayMs,
            RandomDelayMinMs = source.RandomDelayMinMs,
            RandomDelayMaxMs = source.RandomDelayMaxMs,
            Text = source.Text,
            MouseX = source.MouseX,
            MouseY = source.MouseY,
            MouseButton = source.MouseButton,
            IsRecordedDelay = source.IsRecordedDelay
        };
    }
}
