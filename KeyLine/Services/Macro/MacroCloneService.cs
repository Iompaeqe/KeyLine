using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public static class MacroCloneService
{
    public static MacroWorkspace CloneWorkspace(MacroWorkspace source)
    {
        return new MacroWorkspace
        {
            ProfileId = source.ProfileId,
            Name = source.Name,
            Document = CloneDocument(source.Document),
            LoopCount = source.LoopCount,
            TimerMs = source.TimerMs,
            BaseDelayMs = source.BaseDelayMs,
            LoopMode = source.LoopMode,
            ShortcutKeys = source.ShortcutKeys,
            ShortcutsEnabled = source.ShortcutsEnabled,
            ShortcutTriggerBehavior = source.ShortcutTriggerBehavior,
            TargetWindowSearchName = source.TargetWindowSearchName,
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
            UseTextInputMode = source.UseTextInputMode,
            LoopCount = source.LoopCount,
            BaseDelayMs = source.BaseDelayMs
        };

        foreach (var step in CloneSteps(source.Nodes.Where(step => !step.IsSyntheticDisplayNode)))
            clone.Nodes.Add(step);

        return clone;
    }

    public static List<MacroNode> CloneSteps(IEnumerable<MacroNode> source)
    {
        return CloneSteps(source, remapRepeatBlockIds: false);
    }

    public static List<MacroNode> CloneStepsForPaste(IEnumerable<MacroNode> source)
    {
        return CloneSteps(source, remapRepeatBlockIds: true);
    }

    public static MacroNode CloneStep(MacroNode source)
    {
        return CloneStep(source, repeatBlockIdMap: null, conditionBlockIdMap: null);
    }

    private static List<MacroNode> CloneSteps(IEnumerable<MacroNode> source, bool remapRepeatBlockIds)
    {
        var repeatBlockIdMap = remapRepeatBlockIds
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : null;
        var conditionBlockIdMap = remapRepeatBlockIds
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : null;

        return source
            .Select(step => CloneStep(step, repeatBlockIdMap, conditionBlockIdMap))
            .ToList();
    }

    private static MacroNode CloneStep(
        MacroNode source,
        Dictionary<string, string>? repeatBlockIdMap,
        Dictionary<string, string>? conditionBlockIdMap)
    {
        return new MacroNode
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
            IsRecordedDelay = source.IsRecordedDelay,
            RepeatBlockId = GetClonedRepeatBlockId(source.RepeatBlockId, repeatBlockIdMap),
            RepeatCount = source.RepeatCount,
            ConditionBlockId = GetClonedRepeatBlockId(source.ConditionBlockId, conditionBlockIdMap),
            ConditionType = source.ConditionType,
            ConditionKeyName = source.ConditionKeyName,
            ConditionVirtualKey = source.ConditionVirtualKey,
            ConditionShortcutKeys = source.ConditionShortcutKeys,
            ConditionPixelX = source.ConditionPixelX,
            ConditionPixelY = source.ConditionPixelY,
            ConditionPixelRed = source.ConditionPixelRed,
            ConditionPixelGreen = source.ConditionPixelGreen,
            ConditionPixelBlue = source.ConditionPixelBlue,
            ConditionPixelTolerance = source.ConditionPixelTolerance,
            ConditionChancePercent = source.ConditionChancePercent,
            ConditionLoopMode = source.ConditionLoopMode,
            ConditionLoopInterval = source.ConditionLoopInterval
        };
    }

    private static string GetClonedRepeatBlockId(
        string sourceBlockId,
        Dictionary<string, string>? repeatBlockIdMap)
    {
        if (repeatBlockIdMap == null || string.IsNullOrWhiteSpace(sourceBlockId))
            return sourceBlockId;

        if (!repeatBlockIdMap.TryGetValue(sourceBlockId, out var clonedBlockId))
        {
            clonedBlockId = Guid.NewGuid().ToString("N");
            repeatBlockIdMap[sourceBlockId] = clonedBlockId;
        }

        return clonedBlockId;
    }
}

