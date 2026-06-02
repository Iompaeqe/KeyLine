using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacroSpammer.Domain;

namespace MacroSpammer.Services.Macro;

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
            Macros = workspaces.Select(ToPersistedWorkspace).ToList()
        };

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }

    public static List<MacroWorkspace> Import(string path)
    {
        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<MacroFile>(json, JsonOptions);
        return file?.Macros.Select(ToWorkspace).ToList() ?? new List<MacroWorkspace>();
    }

    private static PersistedWorkspace ToPersistedWorkspace(MacroWorkspace workspace)
    {
        return new PersistedWorkspace
        {
            Name = workspace.Name,
            ActiveTimelineIndex = workspace.Document.ActiveTimelineIndex,
            LoopCount = Math.Max(0, workspace.LoopCount),
            LoopMode = workspace.LoopMode,
            TimerMs = Math.Max(0, workspace.TimerMs),
            BaseDelayMs = Math.Max(0, workspace.BaseDelayMs),
            ShortcutKeys = workspace.ShortcutKeys,
            TargetWindowSearchName = workspace.TargetWindowSearchName,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList()
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
            IsRecordedDelay = node.IsRecordedDelay
        };
    }

    private static MacroWorkspace ToWorkspace(PersistedWorkspace persisted)
    {
        var workspace = new MacroWorkspace
        {
            Name = string.IsNullOrWhiteSpace(persisted.Name) ? "Imported Macro" : persisted.Name,
            LoopCount = Math.Max(0, persisted.LoopCount),
            LoopMode = GetPersistedLoopMode(persisted),
            TimerMs = Math.Max(0, persisted.TimerMs),
            BaseDelayMs = Math.Max(0, persisted.BaseDelayMs),
            ShortcutKeys = persisted.ShortcutKeys,
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
            IsRecordedDelay = persisted.IsRecordedDelay
        };
    }

    private sealed class MacroFile
    {
        public int Version { get; set; } = 1;
        public List<PersistedWorkspace> Macros { get; set; } = new();
    }

    private sealed class PersistedWorkspace
    {
        public string Name { get; set; } = "";
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public MacroLoopMode LoopMode { get; set; } = MacroLoopMode.Async;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MacroLoopMode? LoopType { get; set; }
        public int TimerMs { get; set; }
        public int BaseDelayMs { get; set; } = 50;
        public string ShortcutKeys { get; set; } = "";
        public string TargetWindowSearchName { get; set; } = "";
        public List<PersistedTimeline> Timelines { get; set; } = new();
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
    }
}

