using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyLine.Domain;

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
            TimerMs = Math.Max(0, workspace.TimerMs),
            BaseDelayMs = Math.Max(0, workspace.BaseDelayMs),
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
            StandardDelayMs = Math.Max(0, timeline.StandardDelayMs),
            ShowKeyUpDown = timeline.ShowKeyUpDown,
            UseTextInputMode = timeline.UseTextInputMode,
            LoopCount = Math.Max(0, timeline.LoopCount),
            BaseDelayMs = Math.Max(0, timeline.BaseDelayMs),
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
            DelayMs = Math.Max(0, node.DelayMs),
            RandomDelayMinMs = Math.Max(0, node.RandomDelayMinMs),
            RandomDelayMaxMs = Math.Max(0, node.RandomDelayMaxMs),
            Text = node.Text,
            MouseX = node.MouseX,
            MouseY = node.MouseY,
            MouseButton = Math.Clamp(node.MouseButton <= 0 ? 1 : node.MouseButton, 1, 5),
            IsRecordedDelay = node.IsRecordedDelay,
            RepeatBlockId = node.RepeatBlockId,
            RepeatCount = Math.Max(0, node.RepeatCount)
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
            TimerMs = Math.Max(0, persisted.TimerMs),
            BaseDelayMs = Math.Max(0, persisted.BaseDelayMs),
            ShortcutKeys = persisted.ShortcutKeys,
            ShortcutsEnabled = persisted.ShortcutsEnabled,
            TargetWindowSearchName = persisted.TargetWindowSearchName
        };

        workspace.Document.Timelines.Clear();
        foreach (var timeline in persisted.Timelines)
            workspace.Document.Timelines.Add(ToTimeline(
                timeline,
                Math.Max(0, persisted.LoopCount),
                Math.Max(0, persisted.BaseDelayMs)));

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
            StandardDelayMs = Math.Max(0, persisted.StandardDelayMs),
            ShowKeyUpDown = persisted.ShowKeyUpDown,
            UseTextInputMode = persisted.UseTextInputMode,
            LoopCount = Math.Max(0, persisted.LoopCount ?? fallbackLoopCount),
            BaseDelayMs = Math.Max(0, persisted.BaseDelayMs ?? fallbackBaseDelayMs)
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

        return new MacroNode
        {
            Type = type,
            KeyName = persisted.KeyName,
            VirtualKey = persisted.VirtualKey,
            DelayMs = Math.Max(0, persisted.DelayMs),
            RandomDelayMinMs = Math.Max(0, persisted.RandomDelayMinMs),
            RandomDelayMaxMs = Math.Max(0, persisted.RandomDelayMaxMs),
            Text = persisted.Text,
            MouseX = Math.Max(0, persisted.MouseX),
            MouseY = Math.Max(0, persisted.MouseY),
            MouseButton = Math.Clamp(persisted.MouseButton <= 0 ? 1 : persisted.MouseButton, 1, 5),
            IsRecordedDelay = persisted.IsRecordedDelay,
            RepeatBlockId = persisted.RepeatBlockId,
            RepeatCount = Math.Max(0, persisted.RepeatCount)
        };
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
        public string ProfileId { get; set; } = MacroProfile.NoProfileId;
        public string Name { get; set; } = "";
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public MacroLoopMode LoopMode { get; set; } = MacroLoopMode.Async;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MacroLoopMode? LoopType { get; set; }
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
    }
}

