using MacroSpammer.Domain;
using MacroSpammer.Services.Macro;

namespace MacroSpammer.Tests;

public sealed class WorkspaceStateRegressionTests : IDisposable
{
    private readonly string? _originalAppData = Environment.GetEnvironmentVariable("APPDATA");
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "MacroSpammer.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CloneWorkspace_PreservesLoopType()
    {
        var source = CreateWorkspace("Source", MacroLoopType.Sync);

        var clone = MacroCloneService.CloneWorkspace(source);

        Assert.Equal(MacroLoopType.Sync, clone.LoopType);
    }

    [Fact]
    public void SaveAndLoad_PreservesLoopType()
    {
        Environment.SetEnvironmentVariable("APPDATA", _appDataRoot);

        var workspace = CreateWorkspace("Saved", MacroLoopType.Sync);
        MacroStateStore.Save(new[] { workspace }, 0, shortcutsEnabled: true, new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Workspaces);
        Assert.Equal(MacroLoopType.Sync, snapshot.Workspaces[0].LoopType);
    }

    [Fact]
    public void ExportAndImport_PreservesLoopType()
    {
        var workspace = CreateWorkspace("Exported", MacroLoopType.Sync);
        var exportPath = Path.Combine(_appDataRoot, "macro.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        Assert.Equal(MacroLoopType.Sync, imported[0].LoopType);
    }

    [Fact]
    public void GetUniqueDuplicateName_UsesCopySuffixAndIncrements()
    {
        var workspaces = new[]
        {
            CreateWorkspace("Macro 1", MacroLoopType.Async),
            CreateWorkspace("Macro 1 - copy", MacroLoopType.Async)
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

    private static MacroWorkspace CreateWorkspace(string name, MacroLoopType loopType)
    {
        return new MacroWorkspace
        {
            Name = name,
            LoopType = loopType
        };
    }
}
