using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Input;
using KeyLine.Services.Timeline;

namespace KeyLine.Services.Macro;

public enum MacroFileKind
{
    Macros,
    Profiles
}

public sealed class MacroFileImportResult
{
    public MacroFileKind Kind { get; init; } = MacroFileKind.Macros;
    public List<MacroWorkspace> Workspaces { get; init; } = new();
    public List<MacroProfile> Profiles { get; init; } = new();
}

public static class MacroFileStore
{
    public const string Extension = ".keyline";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Export(string path, IEnumerable<MacroWorkspace> workspaces)
    {
        var file = new MacroFile
        {
            Kind = MacroFileKind.Macros.ToString(),
            Macros = workspaces.Select(ToPersistedWorkspace).ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    public static void ExportProfiles(
        string path,
        IEnumerable<MacroProfile> profiles,
        IEnumerable<MacroWorkspace> workspaces)
    {
        var profileList = profiles.ToList();
        var profileIds = profileList
            .Select(profile => MacroProfile.NormalizeId(profile.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var file = new MacroFile
        {
            Kind = MacroFileKind.Profiles.ToString(),
            Profiles = profileList.Select(ToPersistedProfile).ToList(),
            Macros = workspaces
                .Where(workspace => profileIds.Contains(MacroProfile.NormalizeId(workspace.ProfileId)))
                .Select(ToPersistedWorkspace)
                .ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    public static List<MacroWorkspace> Import(string path)
    {
        return ImportPackage(path).Workspaces;
    }

    public static MacroFileImportResult ImportPackage(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<MacroFile>(json, JsonOptions);
        if (file == null)
            return new MacroFileImportResult();

        var kind = Enum.TryParse<MacroFileKind>(file.Kind, ignoreCase: true, out var parsedKind)
            ? parsedKind
            : file.Profiles.Count > 0
                ? MacroFileKind.Profiles
                : MacroFileKind.Macros;

        return new MacroFileImportResult
        {
            Kind = kind,
            Profiles = file.Profiles.Select(ToProfile).ToList(),
            Workspaces = file.Macros.Select(ToWorkspace).ToList()
        };
    }

    private static PersistedWorkspace ToPersistedWorkspace(MacroWorkspace workspace)
    {
        return new PersistedWorkspace
        {
            Id = GetPersistedWorkspaceId(workspace.Id),
            ProfileId = MacroProfile.NormalizeId(workspace.ProfileId),
            Name = workspace.Name,
            ActiveTimelineIndex = workspace.Document.ActiveTimelineIndex,
            LoopCount = Math.Max(0, workspace.LoopCount),
            LoopMode = workspace.LoopMode,
            TimerMs = GetPersistedDelayMs(workspace.TimerMs),
            BaseDelayMs = GetPersistedDelayMs(workspace.BaseDelayMs),
            ShortcutTriggerBehavior = GetSafeShortcutTriggerBehavior(workspace.ShortcutTriggerBehavior),
            ShortcutKeys = GetSafeShortcutKeys(workspace.ShortcutKeys, workspace.ShortcutTriggerBehavior),
            ShortcutsEnabled = GetSafeShortcutsEnabled(workspace.ShortcutsEnabled, workspace.ShortcutKeys, workspace.ShortcutTriggerBehavior),
            TargetWindowSearchName = workspace.TargetWindowSearchName,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList(),
            // Sharing export excludes disabled hooks; only enabled hook timelines are written.
            StartHookEnabled = workspace.StartHookEnabled,
            EndHookEnabled = workspace.EndHookEnabled,
            StartHook = workspace.StartHookEnabled ? ToPersistedTimeline(workspace.StartHookTimeline) : null,
            EndHook = workspace.EndHookEnabled ? ToPersistedTimeline(workspace.EndHookTimeline) : null,
            ResetShortcutKeys = workspace.ResetShortcutKeys
        };
    }

    private static PersistedProfile ToPersistedProfile(MacroProfile profile)
    {
        return new PersistedProfile
        {
            Id = MacroProfile.NormalizeId(profile.Id),
            Name = string.IsNullOrWhiteSpace(profile.Name) ? "Profile" : profile.Name.Trim()
        };
    }

    private static PersistedTimeline ToPersistedTimeline(MacroTimeline timeline)
    {
        return new PersistedTimeline
        {
            Name = timeline.Name,
            UseStandardDelay = timeline.UseStandardDelay,
            StandardDelayMs = GetPersistedDelayMs(timeline.StandardDelayMs),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            UseTextInputMode = timeline.UseTextInputMode,
            LoopCount = Math.Max(0, timeline.LoopCount),
            BaseDelayMs = GetPersistedDelayMs(timeline.BaseDelayMs),
            CooldownMs = GetPersistedDelayMs(timeline.CooldownMs),
            Nodes = timeline.Nodes
                .Where(step => !step.IsSyntheticDisplayNode)
                .Select(ToPersistedStep)
                .ToList()
        };
    }

    private static PersistedStep ToPersistedStep(MacroNode node)
    {
        var isDelayNode = IsDelayNodeType(node.Type);
        var (delayMinMs, delayMaxMs) = isDelayNode
            ? GetPersistedDelayRange(node)
            : (GetPersistedDelayMs(node.DelayMs), GetPersistedDelayMs(node.DelayMs));

        return new PersistedStep
        {
            Type = GetPersistedNodeTypeName(node),
            KeyName = node.KeyName,
            VirtualKey = node.VirtualKey,
            DelayMs = delayMinMs,
            RandomDelayMinMs = isDelayNode ? delayMinMs : GetPersistedDelayMs(node.RandomDelayMinMs),
            RandomDelayMaxMs = isDelayNode ? delayMaxMs : GetPersistedDelayMs(node.RandomDelayMaxMs),
            MinDelayMs = isDelayNode ? delayMinMs : null,
            MaxDelayMs = isDelayNode ? delayMaxMs : null,
            Text = node.Text,
            MouseX = node.MouseX,
            MouseY = node.MouseY,
            MouseButton = Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
            MouseWheelDelta = GetPersistedMouseWheelDelta(node.Type, node.MouseWheelDelta),
            MouseScrollAmount = GetPersistedMouseScrollAmount(node.Type, node.MouseScrollAmount, node.MouseWheelDelta),
            SystemLaunchKind = GetPersistedSystemLaunchKind(node.SystemLaunchKind),
            SystemLaunchTarget = node.SystemLaunchTarget,
            SystemVolumeAction = GetPersistedSystemVolumeAction(node.SystemVolumeAction),
            SystemVolumePercent = Math.Clamp(node.SystemVolumePercent, 0, 100),
            RunMacroId = GetPersistedMacroId(node.RunMacroId),
            SystemWaitWindowTitle = GetPersistedSystemWaitWindowTitle(node),
            SystemWaitPollIntervalMs = GetPersistedSystemWaitPollIntervalMs(node.SystemWaitPollIntervalMs),
            SystemTargetWindowTitle = GetPersistedSystemTargetWindowTitle(node),
            WindowReference = GetPersistedWindowReference(node),
            IsRecordedDelay = node.IsRecordedDelay,
            ToggleKeyMode = GetPersistedToggleKeyMode(node.ToggleKeyMode),
            RepeatBlockId = node.RepeatBlockId,
            RepeatCount = Math.Max(0, node.RepeatCount),
            ConditionBlockId = node.ConditionBlockId,
            ConditionType = node.ConditionType,
            ConditionIsInverted = MacroConditionDefinitions.ShouldInvert(node),
            ConditionKeyName = node.ConditionKeyName,
            ConditionVirtualKey = Math.Max(0, node.ConditionVirtualKey),
            ConditionShortcutKeys = ConditionInputGesture.GetGesture(node),
            ConditionPixelX = Math.Max(0, node.ConditionPixelX),
            ConditionPixelY = Math.Max(0, node.ConditionPixelY),
            ConditionPixelRed = Math.Clamp(node.ConditionPixelRed, 0, 255),
            ConditionPixelGreen = Math.Clamp(node.ConditionPixelGreen, 0, 255),
            ConditionPixelBlue = Math.Clamp(node.ConditionPixelBlue, 0, 255),
            ConditionPixelTolerance = Math.Clamp(node.ConditionPixelTolerance, 0, 255),
            ConditionChancePercent = Math.Clamp(node.ConditionChancePercent, 0, 100),
            ConditionLoopMode = node.ConditionLoopMode,
            ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval),
            ConditionMacroId = GetPersistedMacroId(node.ConditionMacroId),
            ConditionTimePassedMs = Math.Max(1, node.ConditionTimePassedMs)
        };
    }

    private static MacroWorkspace ToWorkspace(PersistedWorkspace persisted)
    {
        var workspace = new MacroWorkspace
        {
            Id = GetPersistedWorkspaceId(persisted.Id),
            ProfileId = MacroProfile.NormalizeId(persisted.ProfileId),
            Name = string.IsNullOrWhiteSpace(persisted.Name) ? "Imported Macro" : persisted.Name,
            LoopCount = Math.Max(0, persisted.LoopCount),
            LoopMode = GetPersistedLoopMode(persisted),
            TimerMs = GetPersistedTimerMs(persisted),
            BaseDelayMs = GetPersistedDelayMs(persisted.BaseDelayMs),
            ShortcutTriggerBehavior = GetSafeShortcutTriggerBehavior(persisted.ShortcutTriggerBehavior),
            ShortcutKeys = GetSafeShortcutKeys(persisted.ShortcutKeys, persisted.ShortcutTriggerBehavior),
            ShortcutsEnabled = GetSafeShortcutsEnabled(persisted.ShortcutsEnabled, persisted.ShortcutKeys, persisted.ShortcutTriggerBehavior),
            TargetWindowSearchName = persisted.TargetWindowSearchName
        };

        workspace.Document.Timelines.Clear();
        foreach (var timeline in persisted.Timelines)
            workspace.Document.Timelines.Add(ToTimeline(
                timeline,
                Math.Max(0, persisted.LoopCount),
                GetPersistedDelayMs(persisted.BaseDelayMs)));

        workspace.Document.EnsureTimeline();
        workspace.Document.SelectTimeline(Math.Clamp(
            persisted.ActiveTimelineIndex,
            0,
            workspace.Document.Timelines.Count - 1));

        ApplyPersistedHooks(workspace, persisted);

        return workspace;
    }

    private static void ApplyPersistedHooks(MacroWorkspace workspace, PersistedWorkspace persisted)
    {
        if (persisted.StartHook != null)
            workspace.StartHookTimeline = ToTimeline(persisted.StartHook, fallbackLoopCount: 1, fallbackBaseDelayMs: 50);
        if (persisted.EndHook != null)
            workspace.EndHookTimeline = ToTimeline(persisted.EndHook, fallbackLoopCount: 1, fallbackBaseDelayMs: 50);

        workspace.StartHookTimeline.Name = MacroTimeline.StartHookName;
        workspace.EndHookTimeline.Name = MacroTimeline.EndHookName;

        // A hook is only enabled on import if it was flagged enabled and its timeline was present.
        workspace.StartHookEnabled = persisted.StartHookEnabled && persisted.StartHook != null;
        workspace.EndHookEnabled = persisted.EndHookEnabled && persisted.EndHook != null;
        workspace.ResetShortcutKeys = persisted.ResetShortcutKeys ?? "";
    }

    private static MacroProfile ToProfile(PersistedProfile persisted)
    {
        return new MacroProfile
        {
            Id = MacroProfile.NormalizeId(persisted.Id),
            Name = string.IsNullOrWhiteSpace(persisted.Name) ? "Profile" : persisted.Name.Trim()
        };
    }

    private static MacroTimeline ToTimeline(PersistedTimeline persisted, int fallbackLoopCount, int fallbackBaseDelayMs)
    {
        var timeline = new MacroTimeline
        {
            Name = persisted.Name,
            UseStandardDelay = persisted.UseStandardDelay,
            StandardDelayMs = GetPersistedDelayMs(persisted.StandardDelayMs),
            ShowKeyUpDown = persisted.ShowKeyUpDown,
            UseTextInputMode = persisted.UseTextInputMode,
            LoopCount = Math.Max(0, persisted.LoopCount ?? fallbackLoopCount),
            BaseDelayMs = GetPersistedDelayMs(persisted.BaseDelayMs ?? fallbackBaseDelayMs),
            CooldownMs = GetPersistedDelayMs(persisted.CooldownMs)
        };

        foreach (var step in persisted.Nodes)
            timeline.Nodes.Add(ToStep(step));

        return timeline;
    }

    private static MacroLoopMode GetPersistedLoopMode(PersistedWorkspace persisted)
    {
        var loopMode = persisted.LoopType ?? persisted.LoopMode;
        return Enum.IsDefined(loopMode) ? loopMode : MacroLoopMode.Async;
    }

    private static MacroNode ToStep(PersistedStep persisted)
    {
        var persistedType = GetPersistedNodeType(persisted.Type);
        var type = GetRuntimeNodeType(persistedType);
        var (delayMinMs, delayMaxMs) = GetPersistedDelayRange(persistedType, persisted);
        var mouseButton = Math.Clamp(persisted.MouseButton <= 0 ? 1 : persisted.MouseButton, 1, 5);
        var conditionType = GetPersistedConditionType(persisted.ConditionType);

        return new MacroNode
        {
            Type = type,
            KeyName = GetPersistedStepKeyName(type, persisted.KeyName, mouseButton),
            VirtualKey = persisted.VirtualKey,
            DelayMs = delayMinMs,
            RandomDelayMinMs = delayMinMs,
            RandomDelayMaxMs = delayMaxMs,
            MinDelayMs = delayMinMs,
            MaxDelayMs = delayMaxMs,
            Text = persisted.Text,
            MouseX = Math.Max(0, persisted.MouseX),
            MouseY = Math.Max(0, persisted.MouseY),
            MouseButton = mouseButton,
            MouseWheelDelta = GetPersistedMouseWheelDelta(type, persisted.MouseWheelDelta),
            MouseScrollAmount = GetPersistedMouseScrollAmount(type, persisted.MouseScrollAmount, persisted.MouseWheelDelta),
            SystemLaunchKind = GetPersistedSystemLaunchKind(persisted.SystemLaunchKind),
            SystemLaunchTarget = persisted.SystemLaunchTarget,
            SystemVolumeAction = GetPersistedSystemVolumeAction(persisted.SystemVolumeAction),
            SystemVolumePercent = Math.Clamp(persisted.SystemVolumePercent, 0, 100),
            RunMacroId = GetPersistedMacroId(persisted.RunMacroId),
            SystemWaitWindowTitle = persisted.SystemWaitWindowTitle,
            SystemWaitPollIntervalMs = GetPersistedSystemWaitPollIntervalMs(persisted.SystemWaitPollIntervalMs),
            SystemTargetWindowTitle = persisted.SystemTargetWindowTitle,
            WindowReference = GetPersistedWindowReference(
                type,
                conditionType,
                persisted.WindowReference,
                persisted.SystemWaitWindowTitle,
                persisted.SystemTargetWindowTitle),
            IsRecordedDelay = persisted.IsRecordedDelay,
            ToggleKeyMode = GetPersistedToggleKeyMode(persisted.ToggleKeyMode),
            RepeatBlockId = persisted.RepeatBlockId,
            RepeatCount = Math.Max(0, persisted.RepeatCount),
            ConditionBlockId = persisted.ConditionBlockId,
            ConditionType = conditionType,
            ConditionIsInverted = MacroConditionDefinitions.CanInvert(conditionType) && persisted.ConditionIsInverted,
            ConditionKeyName = persisted.ConditionKeyName,
            ConditionVirtualKey = Math.Max(0, persisted.ConditionVirtualKey),
            ConditionShortcutKeys = persisted.ConditionShortcutKeys,
            ConditionPixelX = Math.Max(0, persisted.ConditionPixelX),
            ConditionPixelY = Math.Max(0, persisted.ConditionPixelY),
            ConditionPixelRed = Math.Clamp(persisted.ConditionPixelRed, 0, 255),
            ConditionPixelGreen = Math.Clamp(persisted.ConditionPixelGreen, 0, 255),
            ConditionPixelBlue = Math.Clamp(persisted.ConditionPixelBlue, 0, 255),
            ConditionPixelTolerance = Math.Clamp(persisted.ConditionPixelTolerance, 0, 255),
            ConditionChancePercent = Math.Clamp(persisted.ConditionChancePercent, 0, 100),
            ConditionLoopMode = GetPersistedConditionLoopMode(persisted.ConditionLoopMode),
            ConditionLoopInterval = Math.Max(1, persisted.ConditionLoopInterval),
            ConditionMacroId = GetPersistedMacroId(persisted.ConditionMacroId),
            ConditionTimePassedMs = Math.Max(1, persisted.ConditionTimePassedMs)
        };
    }

    private static int GetPersistedTimerMs(PersistedWorkspace persisted)
    {
        if (persisted.TimerMs > 0)
            return GetPersistedDelayMs(persisted.TimerMs);

        return GetPersistedDelayMs((long)persisted.TimerMinutes * 60_000);
    }

    private static int GetPersistedDelayMs(long milliseconds) =>
        DelayFormatter.ClampMilliseconds(milliseconds);

    private static string GetPersistedWorkspaceId(string id)
    {
        id = NormalizeMacroId(id);
        return string.IsNullOrWhiteSpace(id)
            ? Guid.NewGuid().ToString("N")
            : id;
    }

    private static string GetPersistedMacroId(string id) => NormalizeMacroId(id);

    private static string NormalizeMacroId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? "" : id.Trim();

    private static string GetPersistedNodeTypeName(MacroNode node) =>
        IsDelayNodeType(node.Type)
            ? nameof(MacroNodeType.Delay)
            : node.Type.ToString();

    private static bool IsDelayNodeType(MacroNodeType type) =>
        type is MacroNodeType.Delay or MacroNodeType.RandomDelay;

    private static (int MinMs, int MaxMs) GetPersistedDelayRange(MacroNode node)
    {
        var (min, max) = node.GetEffectiveDelayRange();
        min = GetPersistedDelayMs(min);
        max = GetPersistedDelayMs(max);

        if (max < min)
            (min, max) = (max, min);

        return (min, max);
    }

    private static (int MinMs, int MaxMs) GetPersistedDelayRange(
        MacroNodeType persistedType,
        PersistedStep persisted)
    {
        int min;
        int max;

        if (persisted.MinDelayMs.HasValue || persisted.MaxDelayMs.HasValue)
        {
            min = GetPersistedDelayMs(persisted.MinDelayMs ?? persisted.MaxDelayMs ?? persisted.DelayMs);
            max = GetPersistedDelayMs(persisted.MaxDelayMs ?? persisted.MinDelayMs ?? persisted.DelayMs);
        }
        else if (persistedType == MacroNodeType.RandomDelay)
        {
            min = GetPersistedDelayMs(persisted.RandomDelayMinMs);
            max = GetPersistedDelayMs(persisted.RandomDelayMaxMs);
        }
        else
        {
            min = GetPersistedDelayMs(persisted.DelayMs);
            max = min;
        }

        if (max < min)
            (min, max) = (max, min);

        return (min, max);
    }

    private static string GetPersistedStepKeyName(MacroNodeType type, string keyName, int mouseButton)
    {
        if (type is MacroNodeType.MouseDown or MacroNodeType.MouseUp)
            return $"M{mouseButton}";

        if (type == MacroNodeType.MouseScrollUp)
            return string.IsNullOrWhiteSpace(keyName) ? "Wheel Up" : keyName;

        if (type == MacroNodeType.MouseScrollDown)
            return string.IsNullOrWhiteSpace(keyName) ? "Wheel Down" : keyName;

        if (type == MacroNodeType.MouseScrollLeft)
            return string.IsNullOrWhiteSpace(keyName) ? "Wheel Left" : keyName;

        if (type == MacroNodeType.MouseScrollRight)
            return string.IsNullOrWhiteSpace(keyName) ? "Wheel Right" : keyName;

        return keyName;
    }

    private static int GetPersistedMouseWheelDelta(MacroNodeType type, int wheelDelta)
    {
        if (type == MacroNodeType.MouseScrollUp)
            return NativeMethods.WHEEL_DELTA;

        if (type == MacroNodeType.MouseScrollDown)
            return -NativeMethods.WHEEL_DELTA;

        if (type == MacroNodeType.MouseScrollLeft)
            return -NativeMethods.WHEEL_DELTA;

        if (type == MacroNodeType.MouseScrollRight)
            return NativeMethods.WHEEL_DELTA;

        return 0;
    }

    private static int GetPersistedMouseScrollAmount(MacroNodeType type, int amount, int wheelDelta)
    {
        if (type is not (MacroNodeType.MouseScrollUp or
            MacroNodeType.MouseScrollDown or
            MacroNodeType.MouseScrollLeft or
            MacroNodeType.MouseScrollRight))
        {
            return 1;
        }

        if (amount > 0)
            return Math.Clamp(amount, 1, 100);

        return Math.Clamp(Math.Abs(wheelDelta) / NativeMethods.WHEEL_DELTA, 1, 100);
    }

    private static SystemLaunchKind GetPersistedSystemLaunchKind(SystemLaunchKind kind) =>
        Enum.IsDefined(kind) ? kind : SystemLaunchKind.Application;

    private static SystemVolumeAction GetPersistedSystemVolumeAction(SystemVolumeAction action) =>
        Enum.IsDefined(action) ? action : SystemVolumeAction.VolumeUp;

    private static int GetPersistedSystemWaitPollIntervalMs(int milliseconds) =>
        Math.Clamp(milliseconds <= 0 ? 250 : milliseconds, 50, 10_000);

    private static string GetPersistedSystemWaitWindowTitle(MacroNode node)
    {
        if (node.Type != MacroNodeType.SystemWaitUntilWindowOpens)
            return node.SystemWaitWindowTitle;

        var reference = node.GetEffectiveWindowReference();
        return reference.Type == WindowReferenceType.CustomTitle
            ? reference.CustomTitle
            : "";
    }

    private static string GetPersistedSystemTargetWindowTitle(MacroNode node)
    {
        if (node.Type != MacroNodeType.SystemSelectTargetWindow)
            return node.SystemTargetWindowTitle;

        var reference = node.GetEffectiveWindowReference();
        return reference.Type == WindowReferenceType.CustomTitle
            ? reference.CustomTitle
            : "";
    }

    private static WindowReference? GetPersistedWindowReference(MacroNode node)
    {
        if (!UsesWindowReference(node))
            return null;

        var reference = node.GetEffectiveWindowReference();
        reference.CustomTitle = (reference.CustomTitle ?? "").Trim();
        return reference;
    }

    private static WindowReference GetPersistedWindowReference(
        MacroNodeType type,
        MacroConditionType conditionType,
        WindowReference? persistedReference,
        string systemWaitWindowTitle,
        string systemTargetWindowTitle)
    {
        if (!UsesWindowReference(type, conditionType))
            return new WindowReference();

        if (persistedReference != null && Enum.IsDefined(persistedReference.Type))
        {
            var reference = persistedReference.Clone();
            reference.CustomTitle = (reference.CustomTitle ?? "").Trim();
            if (reference.Type == WindowReferenceType.CustomTitle &&
                string.IsNullOrWhiteSpace(reference.CustomTitle))
            {
                reference.CustomTitle = GetLegacyWindowTitle(type, systemWaitWindowTitle, systemTargetWindowTitle);
            }

            return reference;
        }

        if (type == MacroNodeType.SystemFocusWindow)
        {
            return new WindowReference
            {
                Type = WindowReferenceType.SelectedTarget
            };
        }

        if (type == MacroNodeType.ConditionStart && conditionType == MacroConditionType.WindowExists)
        {
            return new WindowReference
            {
                Type = WindowReferenceType.SelectedTarget
            };
        }

        return WindowReference.Custom(GetLegacyWindowTitle(type, systemWaitWindowTitle, systemTargetWindowTitle));
    }

    private static string GetLegacyWindowTitle(
        MacroNodeType type,
        string systemWaitWindowTitle,
        string systemTargetWindowTitle)
    {
        var title = type == MacroNodeType.SystemSelectTargetWindow
            ? systemTargetWindowTitle
            : systemWaitWindowTitle;

        return (title ?? "").Trim();
    }

    private static bool UsesWindowReference(MacroNode node) =>
        UsesWindowReference(node.Type, node.ConditionType);

    private static bool UsesWindowReference(MacroNodeType type, MacroConditionType conditionType) =>
        type is MacroNodeType.SystemWaitUntilWindowOpens or
            MacroNodeType.SystemSelectTargetWindow or
            MacroNodeType.SystemFocusWindow ||
        type == MacroNodeType.ConditionStart && conditionType == MacroConditionType.WindowExists;

    private static MacroNodeType GetPersistedNodeType(string type)
    {
        if (!Enum.TryParse<MacroNodeType>(type, ignoreCase: true, out var parsedType))
            return MacroNodeType.Delay;

        return Enum.IsDefined(parsedType) ? parsedType : MacroNodeType.Delay;
    }

    private static MacroNodeType GetRuntimeNodeType(MacroNodeType persistedType) =>
        persistedType == MacroNodeType.RandomDelay
            ? MacroNodeType.Delay
            : persistedType;

    private static MacroConditionType GetPersistedConditionType(MacroConditionType type) =>
        Enum.IsDefined(type) ? type : MacroConditionType.KeyState;

    private static MacroConditionLoopMode GetPersistedConditionLoopMode(MacroConditionLoopMode mode) =>
        Enum.IsDefined(mode) ? mode : MacroConditionLoopMode.FirstLoop;

    private static ToggleKeyMode GetPersistedToggleKeyMode(ToggleKeyMode mode) =>
        Enum.IsDefined(mode) ? mode : ToggleKeyMode.Normal;

    private static ShortcutTriggerBehavior GetSafeShortcutTriggerBehavior(ShortcutTriggerBehavior behavior) =>
        Enum.IsDefined(behavior) ? behavior : ShortcutTriggerBehavior.PassThrough;

    private static string GetSafeShortcutKeys(string shortcutKeys, ShortcutTriggerBehavior behavior)
    {
        return GetSafeShortcutTriggerBehavior(behavior) == ShortcutTriggerBehavior.RemapConsume &&
               !ShortcutGesture.IsSingleKeyboardKeyShortcut(shortcutKeys)
            ? ""
            : shortcutKeys;
    }

    private static bool GetSafeShortcutsEnabled(
        bool shortcutsEnabled,
        string shortcutKeys,
        ShortcutTriggerBehavior behavior)
    {
        return shortcutsEnabled &&
               (GetSafeShortcutTriggerBehavior(behavior) != ShortcutTriggerBehavior.RemapConsume ||
                ShortcutGesture.IsSingleKeyboardKeyShortcut(shortcutKeys));
    }

    private sealed class MacroFile
    {
        public int Version { get; set; } = 1;
        public string Kind { get; set; } = MacroFileKind.Macros.ToString();
        public List<PersistedProfile> Profiles { get; set; } = new();
        public List<PersistedWorkspace> Macros { get; set; } = new();
    }

    private sealed class PersistedWorkspace
    {
        public string Id { get; set; } = "";
        public string ProfileId { get; set; } = MacroProfile.NoProfileId;
        public string Name { get; set; } = "";
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public MacroLoopMode LoopMode { get; set; } = MacroLoopMode.Async;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MacroLoopMode? LoopType { get; set; }
        public int TimerMinutes { get; set; }
        public int TimerMs { get; set; }
        public int BaseDelayMs { get; set; } = 50;
        public string ShortcutKeys { get; set; } = "";
        public bool ShortcutsEnabled { get; set; }
        public ShortcutTriggerBehavior ShortcutTriggerBehavior { get; set; } = ShortcutTriggerBehavior.PassThrough;
        public string TargetWindowSearchName { get; set; } = "";
        public List<PersistedTimeline> Timelines { get; set; } = new();
        public bool StartHookEnabled { get; set; }
        public bool EndHookEnabled { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PersistedTimeline? StartHook { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PersistedTimeline? EndHook { get; set; }
        public string ResetShortcutKeys { get; set; } = "";
    }

    private sealed class PersistedProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class PersistedTimeline
    {
        public string Name { get; set; } = "";
        public bool UseStandardDelay { get; set; }
        public int StandardDelayMs { get; set; } = 50;
        public bool ShowKeyUpDown { get; set; } = true;
        public bool UseTextInputMode { get; set; }
        public int? LoopCount { get; set; }
        public int? BaseDelayMs { get; set; }
        public int CooldownMs { get; set; }
        public List<PersistedStep> Nodes { get; set; } = new();
    }

    private sealed class PersistedStep
    {
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
        public string Type { get; set; } = nameof(MacroNodeType.Delay);
        public string KeyName { get; set; } = "";
        public int VirtualKey { get; set; }
        public int DelayMs { get; set; }
        public int RandomDelayMinMs { get; set; }
        public int RandomDelayMaxMs { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MinDelayMs { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxDelayMs { get; set; }
        public string Text { get; set; } = "";
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public int MouseButton { get; set; } = 1;
        public int MouseWheelDelta { get; set; }
        public int MouseScrollAmount { get; set; }
        public SystemLaunchKind SystemLaunchKind { get; set; } = SystemLaunchKind.Application;
        public string SystemLaunchTarget { get; set; } = "";
        public SystemVolumeAction SystemVolumeAction { get; set; } = SystemVolumeAction.VolumeUp;
        public int SystemVolumePercent { get; set; } = 50;
        public string RunMacroId { get; set; } = "";
        public string SystemWaitWindowTitle { get; set; } = "";
        public int SystemWaitPollIntervalMs { get; set; } = 250;
        public string SystemTargetWindowTitle { get; set; } = "";
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WindowReference? WindowReference { get; set; }
        public bool IsRecordedDelay { get; set; }
        public ToggleKeyMode ToggleKeyMode { get; set; } = ToggleKeyMode.Normal;
        public string RepeatBlockId { get; set; } = "";
        public int RepeatCount { get; set; } = 2;
        public string ConditionBlockId { get; set; } = "";
        public MacroConditionType ConditionType { get; set; } = MacroConditionType.KeyState;
        public bool ConditionIsInverted { get; set; }
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
        public string ConditionMacroId { get; set; } = "";
        public int ConditionTimePassedMs { get; set; } = 1000;
    }
}

