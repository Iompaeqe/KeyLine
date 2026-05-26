using System.IO;
using System.Text.Json;
using MacroSpammer.Domain;

namespace MacroSpammer.Services.Macro;

public sealed class MacroStateSnapshot
{
    public List<MacroWorkspace> Workspaces { get; init; } = new();
    public int ActiveWorkspaceIndex { get; init; }
}

public static class MacroStateStore
{
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StateDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacroSpammer");

    private static string StatePath => Path.Combine(StateDirectory, "state.json");

    public static MacroStateSnapshot? Load()
    {
        try
        {
            if (!File.Exists(StatePath))
                return null;

            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (state == null)
                return null;

            var workspaces = state.Workspaces.Count > 0
                ? state.Workspaces.Select(ToWorkspace).ToList()
                : new List<MacroWorkspace> { ToLegacyWorkspace(state) };

            return new MacroStateSnapshot
            {
                Workspaces = workspaces,
                ActiveWorkspaceIndex = Math.Clamp(state.ActiveWorkspaceIndex, 0, workspaces.Count - 1)
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Save(IReadOnlyList<MacroWorkspace> workspaces, int activeWorkspaceIndex)
    {
        var safeWorkspaces = workspaces.Count > 0
            ? workspaces
            : new List<MacroWorkspace> { new() };

        var state = new PersistedState
        {
            Version = CurrentVersion,
            ActiveWorkspaceIndex = Math.Clamp(activeWorkspaceIndex, 0, safeWorkspaces.Count - 1),
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
            Document = ToDocument(persistedWorkspace.Timelines, persistedWorkspace.ActiveTimelineIndex),
            LoopCount = Math.Max(0, persistedWorkspace.LoopCount),
            TimerMinutes = Math.Max(0, persistedWorkspace.TimerMinutes),
            BaseDelayMs = Math.Max(0, persistedWorkspace.BaseDelayMs),
            TargetWindowTitle = persistedWorkspace.TargetWindowTitle,
            TargetChildWindowTitle = persistedWorkspace.TargetChildWindowTitle
        };
    }

    private static MacroWorkspace ToLegacyWorkspace(PersistedState state)
    {
        return new MacroWorkspace
        {
            Name = "Macro 1",
            Document = ToDocument(state.Timelines, state.ActiveTimelineIndex),
            LoopCount = Math.Max(0, state.LoopCount),
            TimerMinutes = Math.Max(0, state.TimerMinutes),
            BaseDelayMs = 50
        };
    }

    private static MacroDocument ToDocument(IReadOnlyList<PersistedTimeline> persistedTimelines, int activeTimelineIndex)
    {
        var document = new MacroDocument();
        document.Timelines.Clear();

        foreach (var persistedTimeline in persistedTimelines)
            document.Timelines.Add(ToTimeline(persistedTimeline));

        document.EnsureTimeline();
        document.SelectTimeline(Math.Clamp(activeTimelineIndex, 0, document.Timelines.Count - 1));

        return document;
    }

    private static MacroTimeline ToTimeline(PersistedTimeline persistedTimeline)
    {
        var timeline = new MacroTimeline
        {
            Name = persistedTimeline.Name,
            UseStandardDelay = persistedTimeline.UseStandardDelay,
            StandardDelayMs = Math.Max(0, persistedTimeline.StandardDelayMs),
            ShowKeyUpDown = persistedTimeline.ShowKeyUpDown
        };

        foreach (var persistedStep in persistedTimeline.Steps)
            timeline.Steps.Add(ToStep(persistedStep));

        return timeline;
    }

    private static MacroStep ToStep(PersistedStep persistedStep)
    {
        return new MacroStep
        {
            Type = Enum.TryParse<MacroStepType>(persistedStep.Type, out var type)
                ? type
                : MacroStepType.Delay,
            KeyName = persistedStep.KeyName,
            VirtualKey = persistedStep.VirtualKey,
            DelayMs = Math.Max(0, persistedStep.DelayMs),
            RandomDelayMinMs = Math.Max(0, persistedStep.RandomDelayMinMs),
            RandomDelayMaxMs = Math.Max(0, persistedStep.RandomDelayMaxMs),
            Text = persistedStep.Text,
            MouseX = persistedStep.MouseX,
            MouseY = persistedStep.MouseY,
            IsRecordedDelay = persistedStep.IsRecordedDelay
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
            Steps = timeline.Steps
                .Where(step => !step.IsSyntheticDisplayStep)
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
            TimerMinutes = Math.Max(0, workspace.TimerMinutes),
            BaseDelayMs = Math.Max(0, workspace.BaseDelayMs),
            TargetWindowTitle = workspace.TargetWindowTitle,
            TargetChildWindowTitle = workspace.TargetChildWindowTitle,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList()
        };
    }

    private static PersistedStep ToPersistedStep(MacroStep step)
    {
        return new PersistedStep
        {
            Type = step.Type.ToString(),
            KeyName = step.KeyName,
            VirtualKey = step.VirtualKey,
            DelayMs = Math.Max(0, step.DelayMs),
            RandomDelayMinMs = Math.Max(0, step.RandomDelayMinMs),
            RandomDelayMaxMs = Math.Max(0, step.RandomDelayMaxMs),
            Text = step.Text,
            MouseX = step.MouseX,
            MouseY = step.MouseY,
            IsRecordedDelay = step.IsRecordedDelay
        };
    }

    private sealed class PersistedState
    {
        public int Version { get; set; }
        public int ActiveWorkspaceIndex { get; set; }
        public List<PersistedWorkspace> Workspaces { get; set; } = new();

        // Legacy single-workspace state from v1.
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public int TimerMinutes { get; set; }
        public List<PersistedTimeline> Timelines { get; set; } = new();
    }

    private sealed class PersistedWorkspace
    {
        public string Name { get; set; } = "";
        public int ActiveTimelineIndex { get; set; }
        public int LoopCount { get; set; }
        public int TimerMinutes { get; set; }
        public int BaseDelayMs { get; set; } = 50;
        public string TargetWindowTitle { get; set; } = "";
        public string TargetChildWindowTitle { get; set; } = "";
        public List<PersistedTimeline> Timelines { get; set; } = new();
    }

    private sealed class PersistedTimeline
    {
        public string Name { get; set; } = "";
        public bool UseStandardDelay { get; set; }
        public int StandardDelayMs { get; set; } = 50;
        public bool ShowKeyUpDown { get; set; } = true;
        public List<PersistedStep> Steps { get; set; } = new();
    }

    private sealed class PersistedStep
    {
        public string Type { get; set; } = nameof(MacroStepType.Delay);
        public string KeyName { get; set; } = "";
        public int VirtualKey { get; set; }
        public int DelayMs { get; set; }
        public int RandomDelayMinMs { get; set; }
        public int RandomDelayMaxMs { get; set; }
        public string Text { get; set; } = "";
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool IsRecordedDelay { get; set; }
    }
}
