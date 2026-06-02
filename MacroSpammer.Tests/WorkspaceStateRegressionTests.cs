using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

namespace MacroSpammer.Tests;

public sealed class WorkspaceStateRegressionTests : IDisposable
{
    private readonly string? _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "MacroSpammer.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CloneWorkspace_PreservesLoopMode()
    {
        var source = CreateWorkspace("Source", MacroLoopMode.Sync);

        var clone = MacroCloneService.CloneWorkspace(source);

        Assert.Equal(MacroLoopMode.Sync, clone.LoopMode);
    }

    [Fact]
    public void SaveAndLoad_PreservesLoopMode()
    {
        Environment.SetEnvironmentVariable("APPDATA", _appDataRoot);

        var workspace = CreateWorkspace("Saved", MacroLoopMode.Sync);
        MacroStateStore.Save(new[] { workspace }, 0, shortcutsEnabled: true, new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Workspaces);
        Assert.Equal(MacroLoopMode.Sync, snapshot.Workspaces[0].LoopMode);
    }

    [Fact]
    public void ExportAndImport_PreservesLoopMode()
    {
        var workspace = CreateWorkspace("Exported", MacroLoopMode.Sync);
        var exportPath = Path.Combine(_appDataRoot, "macro.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        Assert.Equal(MacroLoopMode.Sync, imported[0].LoopMode);
    }

    [Fact]
    public void Load_AcceptsLegacyLoopTypeFields()
    {
        Environment.SetEnvironmentVariable("APPDATA", _appDataRoot);
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
        Environment.SetEnvironmentVariable("APPDATA", _originalAppData);

        if (Directory.Exists(_appDataRoot))
            Directory.Delete(_appDataRoot, recursive: true);
    }

    private static MacroWorkspace CreateWorkspace(string name, MacroLoopMode loopMode)
    {
        return new MacroWorkspace
        {
            Name = name,
            LoopMode = loopMode
        };
    }
}

