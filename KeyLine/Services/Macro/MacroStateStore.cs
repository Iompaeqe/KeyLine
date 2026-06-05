using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyLine.Domain;
using KeyLine.Services.Input;
using KeyLine.Services.Timeline;

namespace KeyLine.Services.Macro;

public sealed class MacroStateSnapshot
{
    public List<MacroWorkspace> Workspaces { get; init; } = new();
    public List<MacroProfile> Profiles { get; init; } = new();
    public int ActiveWorkspaceIndex { get; init; }
    public string ActiveProfileId { get; init; } = MacroProfile.NoProfileId;
    public bool ShortcutsEnabled { get; init; }
    public double MainWindowWidth { get; init; }
    public AppSettings Settings { get; init; } = new();
}

public static class MacroStateStore
{
    private const int CurrentVersion = 3;
    public const string EverythingExportKind = "Everything";
    public const string StateDirectoryOverrideEnvironmentVariable = "KEYLINE_STATE_DIRECTORY";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string StateDirectory => StateDirectoryOverride ??
                                           Path.Combine(
                                               Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                               "KeyLine");

    public static string BackupsDirectory => Path.Combine(StateDirectory, "Backups");

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
            ApplyLegacyShortcutEnabledState(workspaces, state.ShortcutsEnabled);

            var profiles = GetPersistedProfiles(state.Profiles);
            NormalizeWorkspaceProfileIds(workspaces, profiles);
            var activeProfileId = GetPersistedActiveProfileId(state.ActiveProfileId, profiles);

            var settings = state.Settings ?? new AppSettings();
            if (state.Version < 2)
                settings.MergeRepeatedDelayNodes = false;

            return new MacroStateSnapshot
            {
                Workspaces = workspaces,
                Profiles = profiles,
                ActiveWorkspaceIndex = Math.Clamp(state.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
                ActiveProfileId = activeProfileId,
                ShortcutsEnabled = state.ShortcutsEnabled,
                MainWindowWidth = Math.Max(0, state.MainWindowWidth),
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
        AppSettings settings,
        double mainWindowWidth = 0,
        IReadOnlyList<MacroProfile>? profiles = null,
        string activeProfileId = MacroProfile.NoProfileId)
    {
        var safeWorkspaces = workspaces.Count > 0
            ? workspaces
            : new List<MacroWorkspace> { new() };
        var safeProfiles = GetSafePersistedProfiles(profiles ?? Array.Empty<MacroProfile>());

        var state = new PersistedState
        {
            Version = CurrentVersion,
            ActiveWorkspaceIndex = Math.Clamp(activeWorkspaceIndex, 0, safeWorkspaces.Count - 1),
            ActiveProfileId = GetSafeActiveProfileId(activeProfileId, safeProfiles),
            ShortcutsEnabled = shortcutsEnabled,
            MainWindowWidth = Math.Max(0, mainWindowWidth),
            Settings = settings,
            Profiles = safeProfiles.Select(ToPersistedProfile).ToList(),
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

    public static void ExportSnapshot(
        string path,
        IReadOnlyList<MacroWorkspace> workspaces,
        int activeWorkspaceIndex,
        bool shortcutsEnabled,
        AppSettings settings,
        double mainWindowWidth = 0,
        IReadOnlyList<MacroProfile>? profiles = null,
        string activeProfileId = MacroProfile.NoProfileId)
    {
        var safeWorkspaces = workspaces.Count > 0
            ? workspaces
            : new List<MacroWorkspace> { new() };
        var safeProfiles = GetSafePersistedProfiles(profiles ?? Array.Empty<MacroProfile>());

        var state = new PersistedState
        {
            Kind = EverythingExportKind,
            Version = CurrentVersion,
            ActiveWorkspaceIndex = Math.Clamp(activeWorkspaceIndex, 0, safeWorkspaces.Count - 1),
            ActiveProfileId = GetSafeActiveProfileId(activeProfileId, safeProfiles),
            ShortcutsEnabled = shortcutsEnabled,
            MainWindowWidth = Math.Max(0, mainWindowWidth),
            Settings = settings,
            Profiles = safeProfiles.Select(ToPersistedProfile).ToList(),
            Workspaces = safeWorkspaces.Select(ToPersistedWorkspace).ToList()
        };

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static string CreateAutoBackupBeforeImport(
        IReadOnlyList<MacroWorkspace> workspaces,
        int activeWorkspaceIndex,
        bool shortcutsEnabled,
        AppSettings settings,
        double mainWindowWidth = 0,
        IReadOnlyList<MacroProfile>? profiles = null,
        string activeProfileId = MacroProfile.NoProfileId)
    {
        Directory.CreateDirectory(BackupsDirectory);

        var path = GetAvailableAutoBackupPath(DateTime.Now);
        ExportSnapshot(
            path,
            workspaces,
            activeWorkspaceIndex,
            shortcutsEnabled,
            settings,
            mainWindowWidth,
            profiles,
            activeProfileId);

        return path;
    }

    private static string GetAvailableAutoBackupPath(DateTime timestamp)
    {
        var baseName = $"AutoBackup_BeforeImport_{timestamp:yyyy-MM-dd}";
        var path = Path.Combine(BackupsDirectory, baseName + MacroFileStore.Extension);
        if (!File.Exists(path))
            return path;

        for (var i = 2;; i++)
        {
            path = Path.Combine(BackupsDirectory, $"{baseName}_{i}{MacroFileStore.Extension}");
            if (!File.Exists(path))
                return path;
        }
    }

    public static bool IsEverythingExport(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty(nameof(PersistedState.Kind), out var kind) &&
                string.Equals(kind.GetString(), EverythingExportKind, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return root.TryGetProperty(nameof(PersistedState.Settings), out _) &&
                   root.TryGetProperty(nameof(PersistedState.Workspaces), out _);
        }
        catch
        {
            return false;
        }
    }

    public static MacroStateSnapshot? ImportSnapshot(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<PersistedState>(json, JsonOptions);
            if (state == null)
                return null;

            var workspaces = state.Workspaces.Count > 0
                ? state.Workspaces.Select(ToWorkspace).ToList()
                : new List<MacroWorkspace> { ToLegacyWorkspace(state) };
            ApplyLegacyShortcutEnabledState(workspaces, state.ShortcutsEnabled);

            var profiles = GetPersistedProfiles(state.Profiles);
            NormalizeWorkspaceProfileIds(workspaces, profiles);
            var activeProfileId = GetPersistedActiveProfileId(state.ActiveProfileId, profiles);

            var settings = state.Settings ?? new AppSettings();
            if (state.Version < 2)
                settings.MergeRepeatedDelayNodes = false;

            return new MacroStateSnapshot
            {
                Workspaces = workspaces,
                Profiles = profiles,
                ActiveWorkspaceIndex = Math.Clamp(state.ActiveWorkspaceIndex, 0, workspaces.Count - 1),
                ActiveProfileId = activeProfileId,
                ShortcutsEnabled = state.ShortcutsEnabled,
                MainWindowWidth = Math.Max(0, state.MainWindowWidth),
                Settings = settings
            };
        }
        catch
        {
            return null;
        }
    }

    private static MacroWorkspace ToWorkspace(PersistedWorkspace persistedWorkspace)
    {
        return new MacroWorkspace
        {
            ProfileId = MacroProfile.NormalizeId(persistedWorkspace.ProfileId),
            Name = string.IsNullOrWhiteSpace(persistedWorkspace.Name)
                ? "Macro"
                : persistedWorkspace.Name,
            Document = ToDocument(
                persistedWorkspace.Timelines,
                persistedWorkspace.ActiveTimelineIndex,
                Math.Max(0, persistedWorkspace.LoopCount),
                GetPersistedDelayMs(persistedWorkspace.BaseDelayMs)),
            LoopCount = Math.Max(0, persistedWorkspace.LoopCount),
            LoopMode = GetPersistedLoopMode(persistedWorkspace),
            TimerMs = GetPersistedTimerMs(persistedWorkspace),
            BaseDelayMs = GetPersistedDelayMs(persistedWorkspace.BaseDelayMs),
            ShortcutTriggerBehavior = GetSafeShortcutTriggerBehavior(persistedWorkspace.ShortcutTriggerBehavior),
            ShortcutKeys = GetSafeShortcutKeys(persistedWorkspace.ShortcutKeys, persistedWorkspace.ShortcutTriggerBehavior),
            ShortcutsEnabled = GetSafeShortcutsEnabled(persistedWorkspace.ShortcutsEnabled, persistedWorkspace.ShortcutKeys, persistedWorkspace.ShortcutTriggerBehavior),
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
            TimerMs = GetPersistedTimerMs(state.TimerMs, state.TimerMinutes),
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
            StandardDelayMs = GetPersistedDelayMs(persistedTimeline.StandardDelayMs),
            ShowKeyUpDown = persistedTimeline.ShowKeyUpDown,
            UseTextInputMode = persistedTimeline.UseTextInputMode,
            LoopCount = Math.Max(0, persistedTimeline.LoopCount ?? fallbackLoopCount),
            BaseDelayMs = GetPersistedDelayMs(persistedTimeline.BaseDelayMs ?? fallbackBaseDelayMs)
        };

        foreach (var persistedStep in persistedTimeline.Nodes)
            timeline.Nodes.Add(ToStep(persistedStep));

        return timeline;
    }

    private static MacroNode ToStep(PersistedStep persistedStep)
    {
        var type = GetPersistedNodeType(persistedStep.Type);
        var mouseButton = Math.Clamp(persistedStep.MouseButton <= 0 ? 1 : persistedStep.MouseButton, 1, 5);

        return new MacroNode
        {
            Type = type,
            KeyName = GetPersistedStepKeyName(type, persistedStep.KeyName, mouseButton),
            VirtualKey = persistedStep.VirtualKey,
            DelayMs = GetPersistedDelayMs(persistedStep.DelayMs),
            RandomDelayMinMs = GetPersistedDelayMs(persistedStep.RandomDelayMinMs),
            RandomDelayMaxMs = GetPersistedDelayMs(persistedStep.RandomDelayMaxMs),
            Text = persistedStep.Text,
            MouseX = persistedStep.MouseX,
            MouseY = persistedStep.MouseY,
            MouseButton = mouseButton,
            IsRecordedDelay = persistedStep.IsRecordedDelay,
            RepeatBlockId = persistedStep.RepeatBlockId,
            RepeatCount = Math.Max(0, persistedStep.RepeatCount),
            ConditionBlockId = persistedStep.ConditionBlockId,
            ConditionType = GetPersistedConditionType(persistedStep.ConditionType),
            ConditionKeyName = persistedStep.ConditionKeyName,
            ConditionVirtualKey = Math.Max(0, persistedStep.ConditionVirtualKey),
            ConditionShortcutKeys = persistedStep.ConditionShortcutKeys,
            ConditionPixelX = Math.Max(0, persistedStep.ConditionPixelX),
            ConditionPixelY = Math.Max(0, persistedStep.ConditionPixelY),
            ConditionPixelRed = Math.Clamp(persistedStep.ConditionPixelRed, 0, 255),
            ConditionPixelGreen = Math.Clamp(persistedStep.ConditionPixelGreen, 0, 255),
            ConditionPixelBlue = Math.Clamp(persistedStep.ConditionPixelBlue, 0, 255),
            ConditionPixelTolerance = Math.Clamp(persistedStep.ConditionPixelTolerance, 0, 255),
            ConditionChancePercent = Math.Clamp(persistedStep.ConditionChancePercent, 0, 100),
            ConditionLoopMode = GetPersistedConditionLoopMode(persistedStep.ConditionLoopMode),
            ConditionLoopInterval = Math.Max(1, persistedStep.ConditionLoopInterval)
        };
    }

    private static string GetPersistedStepKeyName(MacroNodeType type, string keyName, int mouseButton)
    {
        if (type is MacroNodeType.MouseDown or MacroNodeType.MouseUp)
            return $"M{mouseButton}";

        return keyName;
    }

    private static MacroNodeType GetPersistedNodeType(string type)
    {
        if (!Enum.TryParse<MacroNodeType>(type, ignoreCase: true, out var parsedType))
            return MacroNodeType.Delay;

        return Enum.IsDefined(parsedType) ? parsedType : MacroNodeType.Delay;
    }

    private static MacroConditionType GetPersistedConditionType(MacroConditionType type) =>
        Enum.IsDefined(type) ? type : MacroConditionType.KeyState;

    private static MacroConditionLoopMode GetPersistedConditionLoopMode(MacroConditionLoopMode mode) =>
        Enum.IsDefined(mode) ? mode : MacroConditionLoopMode.FirstLoop;

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

    private static PersistedWorkspace ToPersistedWorkspace(MacroWorkspace workspace)
    {
        return new PersistedWorkspace
        {
            ProfileId = MacroProfile.NormalizeId(workspace.ProfileId),
            Name = workspace.Name,
            ActiveTimelineIndex = Math.Clamp(
                workspace.Document.ActiveTimelineIndex,
                0,
                Math.Max(0, workspace.Document.Timelines.Count - 1)),
            LoopCount = Math.Max(0, workspace.LoopCount),
            LoopMode = workspace.LoopMode,
            TimerMs = GetPersistedDelayMs(workspace.TimerMs),
            BaseDelayMs = GetPersistedDelayMs(workspace.BaseDelayMs),
            ShortcutTriggerBehavior = GetSafeShortcutTriggerBehavior(workspace.ShortcutTriggerBehavior),
            ShortcutKeys = GetSafeShortcutKeys(workspace.ShortcutKeys, workspace.ShortcutTriggerBehavior),
            ShortcutsEnabled = GetSafeShortcutsEnabled(workspace.ShortcutsEnabled, workspace.ShortcutKeys, workspace.ShortcutTriggerBehavior),
            TargetWindowSearchName = workspace.TargetWindowSearchName,
            TargetWindowHandle = Math.Max(0, workspace.TargetWindowHandle),
            TargetWindowTitle = workspace.TargetWindowTitle,
            TargetChildWindowHandle = Math.Max(0, workspace.TargetChildWindowHandle),
            TargetChildWindowTitle = workspace.TargetChildWindowTitle,
            Timelines = workspace.Document.Timelines.Select(ToPersistedTimeline).ToList()
        };
    }

    private static List<MacroProfile> GetPersistedProfiles(IReadOnlyList<PersistedProfile> persistedProfiles)
    {
        var profiles = persistedProfiles
            .Select(ToProfile)
            .Where(profile => !MacroProfile.IsNoProfile(profile.Id))
            .ToList();

        return GetSafePersistedProfiles(profiles);
    }

    private static List<MacroProfile> GetSafePersistedProfiles(IReadOnlyList<MacroProfile> profiles)
    {
        var safeProfiles = new List<MacroProfile>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            var id = MacroProfile.NormalizeId(profile.Id);
            if (MacroProfile.IsNoProfile(id) || !usedIds.Add(id))
                continue;

            safeProfiles.Add(new MacroProfile
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(profile.Name)
                    ? "Profile"
                    : profile.Name.Trim()
            });
        }

        return safeProfiles;
    }

    private static MacroProfile ToProfile(PersistedProfile persistedProfile)
    {
        return new MacroProfile
        {
            Id = MacroProfile.NormalizeId(persistedProfile.Id),
            Name = string.IsNullOrWhiteSpace(persistedProfile.Name)
                ? "Profile"
                : persistedProfile.Name.Trim()
        };
    }

    private static PersistedProfile ToPersistedProfile(MacroProfile profile)
    {
        return new PersistedProfile
        {
            Id = MacroProfile.NormalizeId(profile.Id),
            Name = profile.Name
        };
    }

    private static void NormalizeWorkspaceProfileIds(
        IEnumerable<MacroWorkspace> workspaces,
        IReadOnlyList<MacroProfile> profiles)
    {
        var profileIds = profiles
            .Select(profile => profile.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var workspace in workspaces)
        {
            var profileId = MacroProfile.NormalizeId(workspace.ProfileId);
            workspace.ProfileId = MacroProfile.IsNoProfile(profileId) || profileIds.Contains(profileId)
                ? profileId
                : MacroProfile.NoProfileId;
        }
    }

    private static string GetPersistedActiveProfileId(
        string activeProfileId,
        IReadOnlyList<MacroProfile> profiles)
    {
        return GetSafeActiveProfileId(activeProfileId, profiles);
    }

    private static string GetSafeActiveProfileId(
        string activeProfileId,
        IReadOnlyList<MacroProfile> profiles)
    {
        var normalizedId = MacroProfile.NormalizeId(activeProfileId);
        if (MacroProfile.IsNoProfile(normalizedId))
            return MacroProfile.NoProfileId;

        return profiles.Any(profile => string.Equals(profile.Id, normalizedId, StringComparison.OrdinalIgnoreCase))
            ? normalizedId
            : MacroProfile.NoProfileId;
    }

    private static int GetPersistedTimerMs(PersistedWorkspace persistedWorkspace)
    {
        if (persistedWorkspace.TimerMs > 0)
            return GetPersistedDelayMs(persistedWorkspace.TimerMs);

        return GetPersistedDelayMs((long)persistedWorkspace.TimerMinutes * 60_000);
    }

    private static int GetPersistedTimerMs(int timerMs, int timerMinutes)
    {
        if (timerMs > 0)
            return GetPersistedDelayMs(timerMs);

        return GetPersistedDelayMs((long)timerMinutes * 60_000);
    }

    private static int GetPersistedDelayMs(long milliseconds) =>
        DelayFormatter.ClampMilliseconds(milliseconds);

    private static void ApplyLegacyShortcutEnabledState(
        IEnumerable<MacroWorkspace> workspaces,
        bool legacyShortcutsEnabled)
    {
        if (!legacyShortcutsEnabled)
            return;

        foreach (var workspace in workspaces.Where(workspace =>
                     !workspace.ShortcutsEnabled &&
                     !string.IsNullOrWhiteSpace(workspace.ShortcutKeys)))
        {
            workspace.ShortcutsEnabled = true;
        }
    }

    private static MacroLoopMode GetPersistedLoopMode(PersistedWorkspace persistedWorkspace)
    {
        var loopMode = persistedWorkspace.LoopType ?? persistedWorkspace.LoopMode;
        return Enum.IsDefined(loopMode) ? loopMode : MacroLoopMode.Async;
    }

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

    private sealed class PersistedState
    {
        public string Kind { get; set; } = "";
        public int Version { get; set; }
        public int ActiveWorkspaceIndex { get; set; }
        public string ActiveProfileId { get; set; } = MacroProfile.NoProfileId;
        public bool ShortcutsEnabled { get; set; }
        public double MainWindowWidth { get; set; }
        public AppSettings? Settings { get; set; }
        public List<PersistedProfile> Profiles { get; set; } = new();
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
        public long TargetWindowHandle { get; set; }
        public string TargetWindowTitle { get; set; } = "";
        public long TargetChildWindowHandle { get; set; }
        public string TargetChildWindowTitle { get; set; } = "";
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
        [JsonConverter(typeof(FlexibleStringJsonConverter))]
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

