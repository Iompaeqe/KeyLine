using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Timeline;

namespace KeyLine.Tests;

public sealed class WorkspaceStateRegressionTests : IDisposable
{
    private readonly string? _originalStateDirectoryOverride =
        Environment.GetEnvironmentVariable(MacroStateStore.StateDirectoryOverrideEnvironmentVariable);
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "KeyLine.Tests", Guid.NewGuid().ToString("N"));

    public WorkspaceStateRegressionTests()
    {
        Environment.SetEnvironmentVariable(MacroStateStore.StateDirectoryOverrideEnvironmentVariable, _appDataRoot);
    }

    [Fact]
    public void CloneWorkspace_PreservesLoopMode()
    {
        var source = CreateWorkspace("Source", MacroLoopMode.Sync, profileId: "profile-a");

        var clone = MacroCloneService.CloneWorkspace(source);

        Assert.Equal(MacroLoopMode.Sync, clone.LoopMode);
        Assert.Equal("profile-a", clone.ProfileId);
    }

    [Fact]
    public void SaveAndLoad_PreservesLoopMode()
    {
        var profile = new MacroProfile
        {
            Id = "profile-a",
            Name = "Gaming"
        };
        var workspace = CreateWorkspace("State Test Macro", MacroLoopMode.Sync, profile.Id);
        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: true,
            new AppSettings(),
            mainWindowWidth: 1234,
            profiles: new[] { profile },
            activeProfileId: profile.Id);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Workspaces);
        Assert.Equal(MacroLoopMode.Sync, snapshot.Workspaces[0].LoopMode);
        Assert.Equal(profile.Id, snapshot.Workspaces[0].ProfileId);
        Assert.Single(snapshot.Profiles);
        Assert.Equal(profile.Id, snapshot.Profiles[0].Id);
        Assert.Equal("Gaming", snapshot.Profiles[0].Name);
        Assert.Equal(profile.Id, snapshot.ActiveProfileId);
        Assert.Equal(1234, snapshot.MainWindowWidth);
    }

    [Fact]
    public void ExportAndImport_PreservesLoopMode()
    {
        var workspace = CreateWorkspace("Exported", MacroLoopMode.Sync, profileId: "profile-a");
        var exportPath = Path.Combine(_appDataRoot, "macro.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        Assert.Equal(MacroLoopMode.Sync, imported[0].LoopMode);
        Assert.Equal("profile-a", imported[0].ProfileId);
    }

    [Fact]
    public void CreateAutoBackupBeforeImport_WritesEverythingExportToBackups()
    {
        var workspace = CreateWorkspace("Backup Source", MacroLoopMode.Chain, profileId: "profile-a");

        var backupPath = MacroStateStore.CreateAutoBackupBeforeImport(
            new[] { workspace },
            activeWorkspaceIndex: 0,
            shortcutsEnabled: true,
            settings: new AppSettings(),
            profiles: new[] { new MacroProfile { Id = "profile-a", Name = "Profile A" } },
            activeProfileId: "profile-a");

        Assert.StartsWith(MacroStateStore.BackupsDirectory, backupPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("AutoBackup_BeforeImport_", Path.GetFileName(backupPath));
        Assert.EndsWith(MacroFileStore.Extension, backupPath);
        Assert.True(File.Exists(backupPath));
        Assert.True(MacroStateStore.IsEverythingExport(backupPath));

        var snapshot = MacroStateStore.ImportSnapshot(backupPath);
        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Workspaces);
        Assert.Equal("Backup Source", snapshot.Workspaces[0].Name);
        Assert.Equal(MacroLoopMode.Chain, snapshot.Workspaces[0].LoopMode);
        Assert.Single(snapshot.Profiles);
    }

    [Fact]
    public void Load_AcceptsLegacyLoopTypeFields()
    {
        Directory.CreateDirectory(MacroStateStore.StateDirectory);
        File.WriteAllText(
            Path.Combine(MacroStateStore.StateDirectory, "state.json"),
            """
            {
              "Version": 2,
              "ActiveWorkspaceIndex": 0,
              "ShortcutsEnabled": false,
              "Settings": {
                "DefaultLoopType": 2
              },
              "Workspaces": [
                {
                  "Name": "Legacy",
                  "ActiveTimelineIndex": 0,
                  "LoopCount": 0,
                  "LoopType": 2,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [
                    {
                      "Name": "T1",
                      "LoopCount": 0,
                      "BaseDelayMs": 50,
                      "Nodes": []
                    }
                  ]
                }
              ]
            }
            """);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.Equal(MacroLoopMode.Cycle, snapshot.Settings.DefaultLoopMode);
        Assert.Single(snapshot.Workspaces);
        Assert.Equal(MacroLoopMode.Cycle, snapshot.Workspaces[0].LoopMode);
    }

    [Fact]
    public void GetUniqueDuplicateName_UsesCopySuffixAndIncrements()
    {
        var workspaces = new[]
        {
            CreateWorkspace("Macro 1", MacroLoopMode.Async),
            CreateWorkspace("Macro 1 - copy", MacroLoopMode.Async)
        };

        var result = WorkspaceNameService.GetUniqueDuplicateName(workspaces, "Macro 1");

        Assert.Equal("Macro 1 - copy 2", result);
    }

    [Fact]
    public void RepeatBlockSelectionRange_IncludesEndpointsAndContents()
    {
        var timeline = new MacroTimeline();
        var before = new MacroNode { Type = MacroNodeType.Text, Text = "before" };
        var (repeatStart, repeatEnd) = TimelineBlockService.CreateRepeatBlock(3);
        var inside = new MacroNode { Type = MacroNodeType.Text, Text = "inside" };
        var after = new MacroNode { Type = MacroNodeType.Text, Text = "after" };

        timeline.Nodes.Add(before);
        timeline.Nodes.Add(repeatStart);
        timeline.Nodes.Add(inside);
        timeline.Nodes.Add(repeatEnd);
        timeline.Nodes.Add(after);

        var selection = TimelineBlockService.GetSelectionNodesForStep(timeline, repeatEnd);

        Assert.Equal(new[] { repeatStart, inside, repeatEnd }, selection);
    }

    [Fact]
    public void CloneStepsForPaste_RemapsRepeatBlockIds()
    {
        var (repeatStart, repeatEnd) = TimelineBlockService.CreateRepeatBlock(4);

        var clones = MacroCloneService.CloneStepsForPaste(new[] { repeatStart, repeatEnd });

        Assert.Equal(2, clones.Count);
        Assert.Equal(MacroNodeType.RepeatStart, clones[0].Type);
        Assert.Equal(MacroNodeType.RepeatEnd, clones[1].Type);
        Assert.Equal(4, clones[0].RepeatCount);
        Assert.NotEqual(repeatStart.RepeatBlockId, clones[0].RepeatBlockId);
        Assert.Equal(clones[0].RepeatBlockId, clones[1].RepeatBlockId);
    }

    [Fact]
    public void SaveAndLoad_PreservesRepeatBlockNodes()
    {
        var workspace = CreateWorkspace("Repeat State", MacroLoopMode.Async);
        var timeline = workspace.Document.ActiveTimeline;
        var (repeatStart, repeatEnd) = TimelineBlockService.CreateRepeatBlock(5);

        timeline.Nodes.Add(repeatStart);
        timeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "inside" });
        timeline.Nodes.Add(repeatEnd);

        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(3, loadedNodes.Count);
        Assert.Equal(MacroNodeType.RepeatStart, loadedNodes[0].Type);
        Assert.Equal(MacroNodeType.RepeatEnd, loadedNodes[2].Type);
        Assert.Equal(5, loadedNodes[0].RepeatCount);
        Assert.Equal(loadedNodes[0].RepeatBlockId, loadedNodes[2].RepeatBlockId);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            MacroStateStore.StateDirectoryOverrideEnvironmentVariable,
            _originalStateDirectoryOverride);

        if (Directory.Exists(_appDataRoot))
            Directory.Delete(_appDataRoot, recursive: true);
    }

    private static MacroWorkspace CreateWorkspace(
        string name,
        MacroLoopMode loopMode,
        string profileId = MacroProfile.NoProfileId)
    {
        return new MacroWorkspace
        {
            ProfileId = profileId,
            Name = name,
            LoopMode = loopMode
        };
    }
}
