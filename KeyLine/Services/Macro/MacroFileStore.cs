using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyLine.Domain;
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
            ProfileId = MacroProfile.NormalizeId(workspace.ProfileId),
            Name = workspace.Name,
            ActiveTimelineIndex = workspace.Document.ActiveTimelineIndex,
            LoopCount = Math.Max(0, workspace.LoopCount),
            LoopMode = workspace.LoopMode,
            TimerMs = GetPersistedDelayMs(workspace.TimerMs),
            BaseDelayMs = GetPersistedDelayMs(workspace.BaseDelayMs),
            ShortcutKeys = workspace.ShortcutKeys,
            ShortcutsEnabled = workspace.ShortcutsEnabled,
            TargetWindowSearchName = workspace.TargetWindowSearchName,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList()
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
            Nodes = timeline.Nodes
                .Where(step => !step.IsSyntheticDisplayNode)
                .Select(ToPersistedStep)
                .ToList()
        };
    }

    private static PersistedStep ToPersistedStep(MacroNode node)
    {
        return new PersistedStep
        {
            Type = node.Type.ToString(),
            KeyName = node.KeyName,
            VirtualKey = node.VirtualKey,
            DelayMs = GetPersistedDelayMs(node.DelayMs),
            RandomDelayMinMs = GetPersistedDelayMs(node.RandomDelayMinMs),
            RandomDelayMaxMs = GetPersistedDelayMs(node.RandomDelayMaxMs),
            Text = node.Text,
            MouseX = node.MouseX,
            MouseY = node.MouseY,
            MouseButton = Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
            IsRecordedDelay = node.IsRecordedDelay,
            RepeatBlockId = node.RepeatBlockId,
            RepeatCount = Math.Max(0, node.RepeatCount),
            ConditionBlockId = node.ConditionBlockId,
            ConditionType = node.ConditionType,
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
            ConditionLoopInterval = Math.Max(1, node.ConditionLoopInterval)
        };
    }

    private static MacroWorkspace ToWorkspace(PersistedWorkspace persisted)
    {
        var workspace = new MacroWorkspace
        {
            ProfileId = MacroProfile.NormalizeId(persisted.ProfileId),
            Name = string.IsNullOrWhiteSpace(persisted.Name) ? "Imported Macro" : persisted.Name,
            LoopCount = Math.Max(0, persisted.LoopCount),
            LoopMode = GetPersistedLoopMode(persisted),
            TimerMs = GetPersistedTimerMs(persisted),
            BaseDelayMs = GetPersistedDelayMs(persisted.BaseDelayMs),
            ShortcutKeys = persisted.ShortcutKeys,
            ShortcutsEnabled = persisted.ShortcutsEnabled,
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

        return workspace;
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
            BaseDelayMs = GetPersistedDelayMs(persisted.BaseDelayMs ?? fallbackBaseDelayMs)
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
        var type = Enum.TryParse<MacroNodeType>(persisted.Type, out var parsed)
            ? parsed
            : MacroNodeType.Delay;
        var mouseButton = Math.Clamp(persisted.MouseButton <= 0 ? 1 : persisted.MouseButton, 1, 5);

        return new MacroNode
        {
            Type = type,
            KeyName = GetPersistedStepKeyName(type, persisted.KeyName, mouseButton),
            VirtualKey = persisted.VirtualKey,
            DelayMs = GetPersistedDelayMs(persisted.DelayMs),
            RandomDelayMinMs = GetPersistedDelayMs(persisted.RandomDelayMinMs),
            RandomDelayMaxMs = GetPersistedDelayMs(persisted.RandomDelayMaxMs),
            Text = persisted.Text,
            MouseX = Math.Max(0, persisted.MouseX),
            MouseY = Math.Max(0, persisted.MouseY),
            MouseButton = mouseButton,
            IsRecordedDelay = persisted.IsRecordedDelay,
            RepeatBlockId = persisted.RepeatBlockId,
            RepeatCount = Math.Max(0, persisted.RepeatCount),
            ConditionBlockId = persisted.ConditionBlockId,
            ConditionType = GetPersistedConditionType(persisted.ConditionType),
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
            ConditionLoopInterval = Math.Max(1, persisted.ConditionLoopInterval)
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

    private static string GetPersistedStepKeyName(MacroNodeType type, string keyName, int mouseButton)
    {
        if (type is MacroNodeType.MouseDown or MacroNodeType.MouseUp)
            return $"M{mouseButton}";

        return keyName;
    }

    private static MacroConditionType GetPersistedConditionType(MacroConditionType type) =>
        Enum.IsDefined(type) ? type : MacroConditionType.KeyState;

    private static MacroConditionLoopMode GetPersistedConditionLoopMode(MacroConditionLoopMode mode) =>
        Enum.IsDefined(mode) ? mode : MacroConditionLoopMode.FirstLoop;

    private sealed class MacroFile
    {
        public int Version { get; set; } = 1;
        public string Kind { get; set; } = MacroFileKind.Macros.ToString();
        public List<PersistedProfile> Profiles { get; set; } = new();
        public List<PersistedWorkspace> Macros { get; set; } = new();
    }

    private sealed class PersistedWorkspace
    {
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
        public string TargetWindowSearchName { get; set; } = "";
        public List<PersistedTimeline> Timelines { get; set; } = new();
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
        public List<PersistedStep> Nodes { get; set; } = new();
    }

    private sealed class PersistedStep
    {
        public string Type { get; set; } = nameof(MacroNodeType.Delay);
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
}

