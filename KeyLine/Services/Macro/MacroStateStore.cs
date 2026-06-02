using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyLine.Domain;

namespace KeyLine.Services.Macro;

public sealed class MacroStateSnapshot
{
    public List<MacroWorkspace> Workspaces { get; init; } = new();
    public int ActiveWorkspaceIndex { get; init; }
    public bool ShortcutsEnabled { get; init; }
    public AppSettings Settings { get; init; } = new();
}

public static class MacroStateStore
{
    private const int CurrentVersion = 2;
    public const string StateDirectoryOverrideEnvironmentVariable = "KEYLINE_STATE_DIRECTORY";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string StateDirectory => StateDirectoryOverride ??
                                           Path.Combine(
                                               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                               "KeyLine");

    private static string LegacyStateDirectory => StateDirectoryOverride == null
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyLine")
        : Path.Combine(StateDirectoryOverride, "LegacyMacroSpammer");

    private static string? StateDirectoryOverride
    {
        get
        {
            var directory = Environment.GetEnvironmentVariable(StateDirectoryOverrideEnvironmentVariable);
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }
    }

    private static string StatePath => Path.Combine(StateDirectory, "state.json");

    private static string LegacyStatePath => Path.Combine(LegacyStateDirectory, "state.json");

    public static MacroStateSnapshot? Load()
    {
        try
        {
            var statePath = File.Exists(StatePath)
                ? StatePath
                : LegacyStatePath;

            if (!File.Exists(statePath))
                return null;

            var json = File.ReadAllText(statePath);
            var state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (state == null)
                return null;

            var workspaces = state.Workspaces.Count > 0
                ? state.Workspaces.Select(ToWorkspace).ToList()
                : new List<MacroWorkspace> { ToLegacyWorkspace(state) };

            var settings = state.Settings ?? new AppSettings();
            if (state.Version < 2)
                settings.MergeRepeatedDelayNodes = false;

            return new MacroStateSnapshot
            {
                Workspaces = workspaces,
                ActiveWorkspaceIndex = Math.Clamp(state.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
                ShortcutsEnabled = state.ShortcutsEnabled,
                Settings = settings
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Save(
        IReadOnlyList<MacroWorkspace> workspaces,
        int activeWorkspaceIndex,
        bool shortcutsEnabled,
        AppSettings settings)
    {
        var safeWorkspaces = workspaces.Count > 0
            ? workspaces
            : new List<MacroWorkspace> { new() };

        var state = new PersistedState
        {
            Version = CurrentVersion,
            ActiveWorkspaceIndex = Math.Clamp(activeWorkspaceIndex, 0, safeWorkspaces.Count - 1),
            ShortcutsEnabled = shortcutsEnabled,
            Settings = settings,
            Workspaces = safeWorkspaces.Select(ToPersistedWorkspace).ToList()
        };

        Directory.CreateDirectory(StateDirectory);

        var json = JsonSerializer.Serialize(state, JsonOptions);
        var tempPath = StatePath + ".tmp";

        File.WriteAllText(tempPath, json);

        if (File.Exists(StatePath))
            File.Replace(tempPath, StatePath, null);
        else
            File.Move(tempPath, StatePath);
    }

    private static MacroWorkspace ToWorkspace(PersistedWorkspace persistedWorkspace)
    {
        return new MacroWorkspace
        {
            Name = string.IsNullOrWhiteSpace(persistedWorkspace.Name)
                ? "Macro"
                : persistedWorkspace.Name,
            Document = ToDocument(
                persistedWorkspace.Timelines,
                persistedWorkspace.ActiveTimelineIndex,
                Math.Max(0, persistedWorkspace.LoopCount),
                Math.Max(0, persistedWorkspace.BaseDelayMs)),
            LoopCount = Math.Max(0, persistedWorkspace.LoopCount),
            LoopMode = GetPersistedLoopMode(persistedWorkspace),
            TimerMs = GetPersistedTimerMs(persistedWorkspace),
            BaseDelayMs = Math.Max(0, persistedWorkspace.BaseDelayMs),
            ShortcutKeys = persistedWorkspace.ShortcutKeys,
            TargetWindowSearchName = persistedWorkspace.TargetWindowSearchName,
            TargetWindowHandle = Math.Max(0, persistedWorkspace.TargetWindowHandle),
            TargetWindowTitle = persistedWorkspace.TargetWindowTitle,
            TargetChildWindowHandle = Math.Max(0, persistedWorkspace.TargetChildWindowHandle),
            TargetChildWindowTitle = persistedWorkspace.TargetChildWindowTitle
        };
    }

    private static MacroWorkspace ToLegacyWorkspace(PersistedState state)
    {
        return new MacroWorkspace
        {
            Name = "Macro 1",
            Document = ToDocument(state.Timelines, state.ActiveTimelineIndex, Math.Max(0, state.LoopCount), 50),
            LoopCount = Math.Max(0, state.LoopCount),
            LoopMode = MacroLoopMode.Async,
            TimerMs = Math.Max(0, state.TimerMs > 0 ? state.TimerMs : state.TimerMinutes * 60_000),
            BaseDelayMs = 50
        };
    }

    private static MacroDocument ToDocument(
        IReadOnlyList<PersistedTimeline> persistedTimelines,
        int activeTimelineIndex,
        int fallbackLoopCount,
        int fallbackBaseDelayMs)
    {
        var document = new MacroDocument();
        document.Timelines.Clear();

        foreach (var persistedTimeline in persistedTimelines)
            document.Timelines.Add(ToTimeline(persistedTimeline, fallbackLoopCount, fallbackBaseDelayMs));

        document.EnsureTimeline();
        document.SelectTimeline(Math.Clamp(activeTimelineIndex, 0, document.Timelines.Count - 1));

        return document;
    }

    private static MacroTimeline ToTimeline(
        PersistedTimeline persistedTimeline,
        int fallbackLoopCount,
        int fallbackBaseDelayMs)
    {
        var timeline = new MacroTimeline
        {
            Name = persistedTimeline.Name,
            UseStandardDelay = persistedTimeline.UseStandardDelay,
            StandardDelayMs = Math.Max(0, persistedTimeline.StandardDelayMs),
            ShowKeyUpDown = persistedTimeline.ShowKeyUpDown,
            UseTextInputMode = persistedTimeline.UseTextInputMode,
            LoopCount = Math.Max(0, persistedTimeline.LoopCount ?? fallbackLoopCount),
            BaseDelayMs = Math.Max(0, persistedTimeline.BaseDelayMs ?? fallbackBaseDelayMs)
        };

        foreach (var persistedStep in persistedTimeline.Nodes)
            timeline.Nodes.Add(ToStep(persistedStep));

        return timeline;
    }

    private static MacroNode ToStep(PersistedStep persistedStep)
    {
        var type = Enum.TryParse<MacroNodeType>(persistedStep.Type, out var parsedType)
            ? parsedType
            : MacroNodeType.Delay;
        var mouseButton = Math.Clamp(persistedStep.MouseButton <= 0 ? 1 : persistedStep.MouseButton, 1, 5);

        return new MacroNode
        {
            Type = type,
            KeyName = GetPersistedStepKeyName(type, persistedStep.KeyName, mouseButton),
            VirtualKey = persistedStep.VirtualKey,
            DelayMs = Math.Max(0, persistedStep.DelayMs),
            RandomDelayMinMs = Math.Max(0, persistedStep.RandomDelayMinMs),
            RandomDelayMaxMs = Math.Max(0, persistedStep.RandomDelayMaxMs),
            Text = persistedStep.Text,
            MouseX = persistedStep.MouseX,
            MouseY = persistedStep.MouseY,
            MouseButton = mouseButton,
            IsRecordedDelay = persistedStep.IsRecordedDelay
        };
    }

    private static string GetPersistedStepKeyName(MacroNodeType type, string keyName, int mouseButton)
    {
        if (type is MacroNodeType.MouseDown or MacroNodeType.MouseUp)
            return $"M{mouseButton}";

        return keyName;
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

    private static PersistedWorkspace ToPersistedWorkspace(MacroWorkspace workspace)
    {
        return new PersistedWorkspace
        {
            Name = workspace.Name,
            ActiveTimelineIndex = Math.Clamp(
                workspace.Document.ActiveTimelineIndex,
                0,
                Math.Max(0, workspace.Document.Timelines.Count - 1)),
            LoopCount = Math.Max(0, workspace.LoopCount),
            LoopMode = workspace.LoopMode,
            TimerMs = Math.Max(0, workspace.TimerMs),
            BaseDelayMs = Math.Max(0, workspace.BaseDelayMs),
            ShortcutKeys = workspace.ShortcutKeys,
            TargetWindowSearchName = workspace.TargetWindowSearchName,
            TargetWindowHandle = Math.Max(0, workspace.TargetWindowHandle),
            TargetWindowTitle = workspace.TargetWindowTitle,
            TargetChildWindowHandle = Math.Max(0, workspace.TargetChildWindowHandle),
            TargetChildWindowTitle = workspace.TargetChildWindowTitle,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList()
        };
    }

    private static int GetPersistedTimerMs(PersistedWorkspace persistedWorkspace)
    {
        if (persistedWorkspace.TimerMs > 0)
            return Math.Max(0, persistedWorkspace.TimerMs);

        return Math.Max(0, persistedWorkspace.TimerMinutes * 60_000);
    }

    private static MacroLoopMode GetPersistedLoopMode(PersistedWorkspace persistedWorkspace)
    {
        var loopMode = persistedWorkspace.LoopType ?? persistedWorkspace.LoopMode;
        return Enum.IsDefined(loopMode) ? loopMode : MacroLoopMode.Async;
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

    private sealed class PersistedState
    {
        public int Version { get; set; }
        public int ActiveWorkspaceIndex { get; set; }
        public bool ShortcutsEnabled { get; set; }
        public AppSettings? Settings { get; set; }
        public List<PersistedWorkspace> Workspaces { get; set; } = new();

        // Legacy single-workspace state from v1.
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public int TimerMinutes { get; set; }
        public int TimerMs { get; set; }
        public List<PersistedTimeline> Timelines { get; set; } = new();
    }

    private sealed class PersistedWorkspace
    {
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
        public string TargetWindowSearchName { get; set; } = "";
        public long TargetWindowHandle { get; set; }
        public string TargetWindowTitle { get; set; } = "";
        public long TargetChildWindowHandle { get; set; }
        public string TargetChildWindowTitle { get; set; } = "";
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

