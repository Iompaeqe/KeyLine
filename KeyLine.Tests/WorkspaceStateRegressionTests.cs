using KeyLine.Domain;
using KeyLine.Services.Macro;

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
