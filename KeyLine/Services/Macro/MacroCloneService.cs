using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public static class MacroCloneService
{
    public static MacroWorkspace CloneWorkspace(MacroWorkspace source)
    {
        var cloneId = Guid.NewGuid().ToString("N");
        var macroIdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [source.Id] = cloneId
        };

        return new MacroWorkspace
        {
            Id = cloneId,
            ProfileId = source.ProfileId,
            Name = source.Name,
            Document = CloneDocument(source.Document, macroIdMap),
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
            TargetChildWindowTitle = "",
            StartHookTimeline = CloneTimeline(source.StartHookTimeline, macroIdMap),
            EndHookTimeline = CloneTimeline(source.EndHookTimeline, macroIdMap),
            StartHookEnabled = source.StartHookEnabled,
            EndHookEnabled = source.EndHookEnabled,
            ResetShortcutKeys = source.ResetShortcutKeys
        };
    }

    public static MacroDocument CloneDocument(MacroDocument source)
    {
        return CloneDocument(source, macroIdMap: null);
    }

    private static MacroDocument CloneDocument(
        MacroDocument source,
        IReadOnlyDictionary<string, string>? macroIdMap)
    {
        var clone = new MacroDocument();
        clone.Timelines.Clear();

        foreach (var timeline in source.Timelines)
            clone.Timelines.Add(CloneTimeline(timeline, macroIdMap));

        clone.EnsureTimeline();
        clone.SelectTimeline(Math.Clamp(source.ActiveTimelineIndex, 0, clone.Timelines.Count - 1));
        return clone;
    }

    public static MacroTimeline CloneTimeline(MacroTimeline source)
    {
        return CloneTimeline(source, macroIdMap: null);
    }

    private static MacroTimeline CloneTimeline(
        MacroTimeline source,
        IReadOnlyDictionary<string, string>? macroIdMap)
    {
        var clone = new MacroTimeline
        {
            Name = source.Name,
            UseStandardDelay = source.UseStandardDelay,
            StandardDelayMs = source.StandardDelayMs,
            ShowKeyUpDown = source.ShowKeyUpDown,
            UseTextInputMode = source.UseTextInputMode,
            LoopCount = source.LoopCount,
            BaseDelayMs = source.BaseDelayMs,
            CooldownMs = source.CooldownMs
        };

        foreach (var step in CloneSteps(source.Nodes.Where(step => !step.IsSyntheticDisplayNode), macroIdMap))
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
        return CloneStep(source, repeatBlockIdMap: null, conditionBlockIdMap: null, macroIdMap: null);
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
            .Select(step => CloneStep(step, repeatBlockIdMap, conditionBlockIdMap, macroIdMap: null))
            .ToList();
    }

    private static List<MacroNode> CloneSteps(
        IEnumerable<MacroNode> source,
        IReadOnlyDictionary<string, string>? macroIdMap)
    {
        return source
            .Select(step => CloneStep(step, repeatBlockIdMap: null, conditionBlockIdMap: null, macroIdMap))
            .ToList();
    }

    private static MacroNode CloneStep(
        MacroNode source,
        Dictionary<string, string>? repeatBlockIdMap,
        Dictionary<string, string>? conditionBlockIdMap,
        IReadOnlyDictionary<string, string>? macroIdMap)
    {
        return new MacroNode
        {
            Type = source.Type,
            KeyName = source.KeyName,
            VirtualKey = source.VirtualKey,
            DelayMs = source.DelayMs,
            RandomDelayMinMs = source.RandomDelayMinMs,
            RandomDelayMaxMs = source.RandomDelayMaxMs,
            MinDelayMs = source.MinDelayMs,
            MaxDelayMs = source.MaxDelayMs,
            Text = source.Text,
            MouseX = source.MouseX,
            MouseY = source.MouseY,
            MouseButton = source.MouseButton,
            MouseWheelDelta = source.MouseWheelDelta,
            MouseScrollAmount = source.MouseScrollAmount,
            SystemLaunchKind = source.SystemLaunchKind,
            SystemLaunchTarget = source.SystemLaunchTarget,
            SystemVolumeAction = source.SystemVolumeAction,
            SystemVolumePercent = source.SystemVolumePercent,
            RunMacroId = GetClonedMacroId(source.RunMacroId, macroIdMap),
            SystemWaitWindowTitle = source.SystemWaitWindowTitle,
            SystemWaitPollIntervalMs = source.SystemWaitPollIntervalMs,
            SystemTargetWindowTitle = source.SystemTargetWindowTitle,
            WindowReference = source.GetEffectiveWindowReference().Clone(),
            IsRecordedDelay = source.IsRecordedDelay,
            ToggleKeyMode = source.ToggleKeyMode,
            RepeatBlockId = GetClonedRepeatBlockId(source.RepeatBlockId, repeatBlockIdMap),
            RepeatCount = source.RepeatCount,
            ConditionBlockId = GetClonedRepeatBlockId(source.ConditionBlockId, conditionBlockIdMap),
            ConditionType = source.ConditionType,
            ConditionIsInverted = source.ConditionIsInverted,
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
            ConditionLoopInterval = source.ConditionLoopInterval,
            ConditionMacroId = GetClonedMacroId(source.ConditionMacroId, macroIdMap),
            ConditionTimePassedMs = source.ConditionTimePassedMs
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

    private static string GetClonedMacroId(
        string sourceMacroId,
        IReadOnlyDictionary<string, string>? macroIdMap)
    {
        if (macroIdMap == null || string.IsNullOrWhiteSpace(sourceMacroId))
            return sourceMacroId;

        return macroIdMap.TryGetValue(sourceMacroId.Trim(), out var clonedMacroId)
            ? clonedMacroId
            : sourceMacroId;
    }
}

