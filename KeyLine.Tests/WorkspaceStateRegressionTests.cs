using KeyLine.Domain;
using KeyLine.Services.Input;
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
    public void CloneSteps_PreservesMouseWheelDelta()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.MouseScrollDown,
            KeyName = "Wheel Down",
            MouseWheelDelta = -120,
            MouseScrollAmount = 2
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.MouseScrollDown, clone.Type);
        Assert.Equal("Wheel Down", clone.KeyName);
        Assert.Equal(-120, clone.MouseWheelDelta);
        Assert.Equal(2, clone.MouseScrollAmount);
    }

    [Fact]
    public void CloneSteps_PreservesDelayRange()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.Delay,
            DelayMs = 50,
            MinDelayMs = 50,
            MaxDelayMs = 150,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.Delay, clone.Type);
        Assert.Equal(50, clone.MinDelayMs);
        Assert.Equal(150, clone.MaxDelayMs);
        Assert.Equal((50, 150), clone.GetEffectiveDelayRange());
    }

    [Fact]
    public void CloneSteps_PreservesSystemNodeConfiguration()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.SystemOpenLaunch,
            SystemLaunchKind = SystemLaunchKind.Url,
            SystemLaunchTarget = "https://example.com"
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.SystemOpenLaunch, clone.Type);
        Assert.Equal(SystemLaunchKind.Url, clone.SystemLaunchKind);
        Assert.Equal("https://example.com", clone.SystemLaunchTarget);
    }

    [Fact]
    public void CloneSteps_PreservesSystemWindowWaitConfiguration()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.SystemWaitUntilWindowOpens,
            SystemWaitWindowTitle = "Notepad",
            SystemWaitPollIntervalMs = 375,
            WindowReference = WindowReference.Custom("Notepad")
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.SystemWaitUntilWindowOpens, clone.Type);
        Assert.Equal("Notepad", clone.SystemWaitWindowTitle);
        Assert.Equal(375, clone.SystemWaitPollIntervalMs);
        Assert.Equal(WindowReferenceType.CustomTitle, clone.WindowReference.Type);
        Assert.Equal("Notepad", clone.WindowReference.CustomTitle);
    }

    [Fact]
    public void CloneSteps_PreservesSystemTargetWindowConfiguration()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.SystemSelectTargetWindow,
            SystemTargetWindowTitle = "Game",
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.LastFoundWindow
            }
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.SystemSelectTargetWindow, clone.Type);
        Assert.Equal("Game", clone.SystemTargetWindowTitle);
        Assert.Equal(WindowReferenceType.LastFoundWindow, clone.WindowReference.Type);
    }

    [Fact]
    public void CloneSteps_PreservesSystemFocusWindowConfiguration()
    {
        var source = new MacroNode
        {
            Type = MacroNodeType.SystemFocusWindow,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.FocusedWindow
            }
        };

        var clone = MacroCloneService.CloneStep(source);

        Assert.Equal(MacroNodeType.SystemFocusWindow, clone.Type);
        Assert.Equal(WindowReferenceType.FocusedWindow, clone.WindowReference.Type);
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
    public void SaveAndLoad_PreservesMouseScrollNodes()
    {
        var workspace = CreateWorkspace("State Scroll", MacroLoopMode.Async);
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.MouseScrollUp,
            KeyName = "Wheel Up",
            MouseWheelDelta = 120,
            MouseScrollAmount = 3
        });
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.MouseScrollLeft,
            KeyName = "Wheel Left",
            MouseWheelDelta = -120,
            MouseScrollAmount = 2
        });

        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(2, loadedNodes.Count);
        Assert.Equal(MacroNodeType.MouseScrollUp, loadedNodes[0].Type);
        Assert.Equal(120, loadedNodes[0].MouseWheelDelta);
        Assert.Equal(3, loadedNodes[0].MouseScrollAmount);
        Assert.Equal(MacroNodeType.MouseScrollLeft, loadedNodes[1].Type);
        Assert.Equal(-120, loadedNodes[1].MouseWheelDelta);
        Assert.Equal(2, loadedNodes[1].MouseScrollAmount);
    }

    [Fact]
    public void SaveAndLoad_PreservesDelayRange()
    {
        var workspace = CreateWorkspace("State Delay Range", MacroLoopMode.Async);
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.Delay,
            DelayMs = 50,
            MinDelayMs = 50,
            MaxDelayMs = 150,
            RandomDelayMinMs = 50,
            RandomDelayMaxMs = 150
        });

        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNode = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes[0];
        Assert.Equal(MacroNodeType.Delay, loadedNode.Type);
        Assert.Equal(50, loadedNode.MinDelayMs);
        Assert.Equal(150, loadedNode.MaxDelayMs);
        Assert.Equal((50, 150), loadedNode.GetEffectiveDelayRange());
    }

    [Fact]
    public void SaveAndLoad_PreservesSystemNodes()
    {
        var workspace = CreateWorkspace("State System", MacroLoopMode.Async);
        var timeline = workspace.Document.ActiveTimeline;
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemOpenLaunch,
            SystemLaunchKind = SystemLaunchKind.Folder,
            SystemLaunchTarget = @"C:\Temp"
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemVolumeControl,
            SystemVolumeAction = SystemVolumeAction.SetVolumePercent,
            SystemVolumePercent = 37
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemWaitUntilWindowOpens,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.LastLaunchedWindow
            },
            SystemWaitPollIntervalMs = 300
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemSelectTargetWindow,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.LastFoundWindow
            }
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemFocusWindow,
            WindowReference = WindowReference.Custom("Calculator")
        });

        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(5, loadedNodes.Count);
        Assert.Equal(MacroNodeType.SystemOpenLaunch, loadedNodes[0].Type);
        Assert.Equal(SystemLaunchKind.Folder, loadedNodes[0].SystemLaunchKind);
        Assert.Equal(@"C:\Temp", loadedNodes[0].SystemLaunchTarget);
        Assert.Equal(MacroNodeType.SystemVolumeControl, loadedNodes[1].Type);
        Assert.Equal(SystemVolumeAction.SetVolumePercent, loadedNodes[1].SystemVolumeAction);
        Assert.Equal(37, loadedNodes[1].SystemVolumePercent);
        Assert.Equal(MacroNodeType.SystemWaitUntilWindowOpens, loadedNodes[2].Type);
        Assert.Equal("", loadedNodes[2].SystemWaitWindowTitle);
        Assert.Equal(WindowReferenceType.LastLaunchedWindow, loadedNodes[2].WindowReference.Type);
        Assert.Equal(300, loadedNodes[2].SystemWaitPollIntervalMs);
        Assert.Equal(MacroNodeType.SystemSelectTargetWindow, loadedNodes[3].Type);
        Assert.Equal("", loadedNodes[3].SystemTargetWindowTitle);
        Assert.Equal(WindowReferenceType.LastFoundWindow, loadedNodes[3].WindowReference.Type);
        Assert.Equal(MacroNodeType.SystemFocusWindow, loadedNodes[4].Type);
        Assert.Equal(WindowReferenceType.CustomTitle, loadedNodes[4].WindowReference.Type);
        Assert.Equal("Calculator", loadedNodes[4].WindowReference.CustomTitle);
    }

    [Fact]
    public void Load_MigratesLegacySystemWindowTitlesToCustomWindowReferences()
    {
        Directory.CreateDirectory(MacroStateStore.StateDirectory);
        File.WriteAllText(
            Path.Combine(MacroStateStore.StateDirectory, "state.json"),
            """
            {
              "Version": 3,
              "ActiveWorkspaceIndex": 0,
              "ShortcutsEnabled": false,
              "Settings": {},
              "Workspaces": [
                {
                  "Name": "Legacy Window Titles",
                  "ActiveTimelineIndex": 0,
                  "LoopMode": 0,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [
                    {
                      "Name": "T1",
                      "LoopCount": 0,
                      "BaseDelayMs": 50,
                      "Nodes": [
                        {
                          "Type": "SystemWaitUntilWindowOpens",
                          "SystemWaitWindowTitle": "Discord",
                          "SystemWaitPollIntervalMs": 300
                        },
                        {
                          "Type": "SystemSelectTargetWindow",
                          "SystemTargetWindowTitle": "Discord"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(WindowReferenceType.CustomTitle, loadedNodes[0].WindowReference.Type);
        Assert.Equal("Discord", loadedNodes[0].WindowReference.CustomTitle);
        Assert.Equal(WindowReferenceType.CustomTitle, loadedNodes[1].WindowReference.Type);
        Assert.Equal("Discord", loadedNodes[1].WindowReference.CustomTitle);
    }

    [Fact]
    public void Load_MigratesLegacyRandomDelayToDelayRange()
    {
        Directory.CreateDirectory(MacroStateStore.StateDirectory);
        File.WriteAllText(
            Path.Combine(MacroStateStore.StateDirectory, "state.json"),
            """
            {
              "Version": 3,
              "ActiveWorkspaceIndex": 0,
              "ShortcutsEnabled": false,
              "Settings": {},
              "Workspaces": [
                {
                  "Name": "Legacy Random Delay",
                  "ActiveTimelineIndex": 0,
                  "LoopMode": 0,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [
                    {
                      "Name": "T1",
                      "LoopCount": 0,
                      "BaseDelayMs": 50,
                      "Nodes": [
                        {
                          "Type": "RandomDelay",
                          "RandomDelayMinMs": 50,
                          "RandomDelayMaxMs": 150
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNode = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes[0];
        Assert.Equal(MacroNodeType.Delay, loadedNode.Type);
        Assert.Equal(50, loadedNode.MinDelayMs);
        Assert.Equal(150, loadedNode.MaxDelayMs);
        Assert.Equal((50, 150), loadedNode.GetEffectiveDelayRange());
    }

    [Fact]
    public void AppSettings_DefaultsGlobalRemapEnabledWithToggleShortcut()
    {
        var settings = new AppSettings();

        Assert.True(settings.GlobalRemapEnabled);
        Assert.Equal("Ctrl + Shift + Alt + R", ShortcutGesture.Format(settings.ToggleGlobalRemapShortcut));
    }

    [Fact]
    public void SaveAndLoad_PreservesGlobalRemapSettings()
    {
        var settings = new AppSettings
        {
            GlobalRemapEnabled = false,
            ToggleGlobalRemapShortcut = "17,18,82"
        };

        MacroStateStore.Save(
            new[] { CreateWorkspace("State Test Macro", MacroLoopMode.Async) },
            0,
            shortcutsEnabled: false,
            settings);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.False(snapshot.Settings.GlobalRemapEnabled);
        Assert.Equal("17,18,82", snapshot.Settings.ToggleGlobalRemapShortcut);
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
    public void ExportAndImport_PreservesMouseScrollNodes()
    {
        var workspace = CreateWorkspace("Export Scroll", MacroLoopMode.Async);
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.MouseScrollDown,
            KeyName = "Wheel Down",
            MouseWheelDelta = -120,
            MouseScrollAmount = 4
        });
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.MouseScrollRight,
            KeyName = "Wheel Right",
            MouseWheelDelta = 120,
            MouseScrollAmount = 5
        });
        var exportPath = Path.Combine(_appDataRoot, "scroll.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        var loadedNodes = imported[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(2, loadedNodes.Count);
        Assert.Equal(MacroNodeType.MouseScrollDown, loadedNodes[0].Type);
        Assert.Equal(-120, loadedNodes[0].MouseWheelDelta);
        Assert.Equal(4, loadedNodes[0].MouseScrollAmount);
        Assert.Equal(MacroNodeType.MouseScrollRight, loadedNodes[1].Type);
        Assert.Equal(120, loadedNodes[1].MouseWheelDelta);
        Assert.Equal(5, loadedNodes[1].MouseScrollAmount);
    }

    [Fact]
    public void ExportAndImport_PreservesDelayRange()
    {
        var workspace = CreateWorkspace("Export Delay Range", MacroLoopMode.Async);
        workspace.Document.ActiveTimeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.Delay,
            DelayMs = 75,
            MinDelayMs = 75,
            MaxDelayMs = 250,
            RandomDelayMinMs = 75,
            RandomDelayMaxMs = 250
        });
        var exportPath = Path.Combine(_appDataRoot, "delay-range.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        var loadedNode = imported[0].Document.ActiveTimeline.Nodes[0];
        Assert.Equal(MacroNodeType.Delay, loadedNode.Type);
        Assert.Equal(75, loadedNode.MinDelayMs);
        Assert.Equal(250, loadedNode.MaxDelayMs);
        Assert.Equal((75, 250), loadedNode.GetEffectiveDelayRange());
    }

    [Fact]
    public void Import_MigratesLegacyRandomDelayToDelayRange()
    {
        var importPath = Path.Combine(_appDataRoot, "legacy-random.keyline");
        Directory.CreateDirectory(_appDataRoot);
        File.WriteAllText(
            importPath,
            """
            {
              "Kind": "Macros",
              "Macros": [
                {
                  "Name": "Legacy Random Delay",
                  "ActiveTimelineIndex": 0,
                  "LoopMode": 0,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [
                    {
                      "Name": "T1",
                      "LoopCount": 0,
                      "BaseDelayMs": 50,
                      "Nodes": [
                        {
                          "Type": "RandomDelay",
                          "RandomDelayMinMs": 20,
                          "RandomDelayMaxMs": 120
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var imported = MacroFileStore.Import(importPath);

        Assert.Single(imported);
        var loadedNode = imported[0].Document.ActiveTimeline.Nodes[0];
        Assert.Equal(MacroNodeType.Delay, loadedNode.Type);
        Assert.Equal(20, loadedNode.MinDelayMs);
        Assert.Equal(120, loadedNode.MaxDelayMs);
        Assert.Equal((20, 120), loadedNode.GetEffectiveDelayRange());
    }

    [Fact]
    public void ExportAndImport_PreservesSystemNodes()
    {
        var workspace = CreateWorkspace("Export System", MacroLoopMode.Async);
        var timeline = workspace.Document.ActiveTimeline;
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemOpenLaunch,
            SystemLaunchKind = SystemLaunchKind.File,
            SystemLaunchTarget = @"C:\Temp\readme.txt"
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemVolumeControl,
            SystemVolumeAction = SystemVolumeAction.MuteToggle,
            SystemVolumePercent = 50
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemWaitUntilWindowOpens,
            WindowReference = WindowReference.Custom("Launcher"),
            SystemWaitPollIntervalMs = 400
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemSelectTargetWindow,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.LastFoundWindow
            }
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.SystemFocusWindow,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.SelectedTarget
            }
        });
        var exportPath = Path.Combine(_appDataRoot, "system.keyline");
        Directory.CreateDirectory(_appDataRoot);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        var loadedNodes = imported[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(5, loadedNodes.Count);
        Assert.Equal(MacroNodeType.SystemOpenLaunch, loadedNodes[0].Type);
        Assert.Equal(SystemLaunchKind.File, loadedNodes[0].SystemLaunchKind);
        Assert.Equal(@"C:\Temp\readme.txt", loadedNodes[0].SystemLaunchTarget);
        Assert.Equal(MacroNodeType.SystemVolumeControl, loadedNodes[1].Type);
        Assert.Equal(SystemVolumeAction.MuteToggle, loadedNodes[1].SystemVolumeAction);
        Assert.Equal(MacroNodeType.SystemWaitUntilWindowOpens, loadedNodes[2].Type);
        Assert.Equal("Launcher", loadedNodes[2].SystemWaitWindowTitle);
        Assert.Equal(WindowReferenceType.CustomTitle, loadedNodes[2].WindowReference.Type);
        Assert.Equal("Launcher", loadedNodes[2].WindowReference.CustomTitle);
        Assert.Equal(400, loadedNodes[2].SystemWaitPollIntervalMs);
        Assert.Equal(MacroNodeType.SystemSelectTargetWindow, loadedNodes[3].Type);
        Assert.Equal("", loadedNodes[3].SystemTargetWindowTitle);
        Assert.Equal(WindowReferenceType.LastFoundWindow, loadedNodes[3].WindowReference.Type);
        Assert.Equal(MacroNodeType.SystemFocusWindow, loadedNodes[4].Type);
        Assert.Equal(WindowReferenceType.SelectedTarget, loadedNodes[4].WindowReference.Type);
    }

    [Fact]
    public void ExportAndImport_PreservesConditionBlockNodes()
    {
        var workspace = CreateWorkspace("Condition Export", MacroLoopMode.Async);
        var timeline = workspace.Document.ActiveTimeline;
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();
        conditionStart.ConditionType = MacroConditionType.PixelColor;
        conditionStart.ConditionPixelX = 24;
        conditionStart.ConditionPixelY = 48;
        conditionStart.ConditionPixelRed = 12;
        conditionStart.ConditionPixelGreen = 34;
        conditionStart.ConditionPixelBlue = 56;
        conditionStart.ConditionPixelTolerance = 5;
        conditionStart.ConditionShortcutKeys = ConditionInputGesture.Serialize(new[]
        {
            0x11,
            0x12,
            0x51
        });
        var exportPath = Path.Combine(_appDataRoot, "condition.keyline");
        Directory.CreateDirectory(_appDataRoot);

        timeline.Nodes.Add(conditionStart);
        timeline.Nodes.Add(conditionEnd);

        MacroFileStore.Export(exportPath, new[] { workspace });

        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        var loadedNodes = imported[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(MacroNodeType.ConditionStart, loadedNodes[0].Type);
        Assert.Equal(MacroNodeType.ConditionEnd, loadedNodes[1].Type);
        Assert.Equal(MacroConditionType.PixelColor, loadedNodes[0].ConditionType);
        Assert.Equal(24, loadedNodes[0].ConditionPixelX);
        Assert.Equal(48, loadedNodes[0].ConditionPixelY);
        Assert.Equal(12, loadedNodes[0].ConditionPixelRed);
        Assert.Equal(34, loadedNodes[0].ConditionPixelGreen);
        Assert.Equal(56, loadedNodes[0].ConditionPixelBlue);
        Assert.Equal(5, loadedNodes[0].ConditionPixelTolerance);
        Assert.Equal(conditionStart.ConditionShortcutKeys, loadedNodes[0].ConditionShortcutKeys);
        Assert.Equal(loadedNodes[0].ConditionBlockId, loadedNodes[1].ConditionBlockId);
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
    public void Load_AcceptsNumericNodeTypeFields()
    {
        Assert.Equal(12, (int)MacroNodeType.RepeatStart);
        Assert.Equal(13, (int)MacroNodeType.RepeatEnd);

        Directory.CreateDirectory(MacroStateStore.StateDirectory);
        File.WriteAllText(
            Path.Combine(MacroStateStore.StateDirectory, "state.json"),
            """
            {
              "Version": 3,
              "ActiveWorkspaceIndex": 0,
              "ShortcutsEnabled": false,
              "Settings": {},
              "Workspaces": [
                {
                  "Name": "Numeric Node Types",
                  "ActiveTimelineIndex": 0,
                  "LoopMode": 0,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [
                    {
                      "Name": "T1",
                      "LoopCount": 0,
                      "BaseDelayMs": 50,
                      "Nodes": [
                        {
                          "Type": 12,
                          "RepeatBlockId": "repeat-a",
                          "RepeatCount": 3
                        },
                        {
                          "Type": 13,
                          "RepeatBlockId": "repeat-a"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(2, loadedNodes.Count);
        Assert.Equal(MacroNodeType.RepeatStart, loadedNodes[0].Type);
        Assert.Equal(MacroNodeType.RepeatEnd, loadedNodes[1].Type);
        Assert.Equal("repeat-a", loadedNodes[0].RepeatBlockId);
        Assert.Equal(3, loadedNodes[0].RepeatCount);
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

    [Fact]
    public void ConditionBlockSelectionRange_IncludesEndpointsAndContents()
    {
        var timeline = new MacroTimeline();
        var before = new MacroNode { Type = MacroNodeType.Text, Text = "before" };
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();
        var inside = new MacroNode { Type = MacroNodeType.Text, Text = "inside" };
        var after = new MacroNode { Type = MacroNodeType.Text, Text = "after" };

        timeline.Nodes.Add(before);
        timeline.Nodes.Add(conditionStart);
        timeline.Nodes.Add(inside);
        timeline.Nodes.Add(conditionEnd);
        timeline.Nodes.Add(after);

        var selection = TimelineBlockService.GetSelectionNodesForStep(timeline, conditionEnd);

        Assert.Equal(new[] { conditionStart, inside, conditionEnd }, selection);
    }

    [Fact]
    public void CloneStepsForPaste_RemapsConditionBlockIds()
    {
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();
        conditionStart.ConditionType = MacroConditionType.RandomChance;
        conditionStart.ConditionChancePercent = 30;
        conditionStart.ConditionShortcutKeys = ConditionInputGesture.Serialize(new[]
        {
            0x11,
            0x12,
            0x51
        });

        var clones = MacroCloneService.CloneStepsForPaste(new[] { conditionStart, conditionEnd });

        Assert.Equal(2, clones.Count);
        Assert.Equal(MacroNodeType.ConditionStart, clones[0].Type);
        Assert.Equal(MacroNodeType.ConditionEnd, clones[1].Type);
        Assert.Equal(MacroConditionType.RandomChance, clones[0].ConditionType);
        Assert.Equal(30, clones[0].ConditionChancePercent);
        Assert.Equal(conditionStart.ConditionShortcutKeys, clones[0].ConditionShortcutKeys);
        Assert.NotEqual(conditionStart.ConditionBlockId, clones[0].ConditionBlockId);
        Assert.Equal(clones[0].ConditionBlockId, clones[1].ConditionBlockId);
    }

    [Fact]
    public void SaveAndLoad_PreservesConditionBlockNodes()
    {
        var workspace = CreateWorkspace("Condition State", MacroLoopMode.Async);
        var timeline = workspace.Document.ActiveTimeline;
        var (conditionStart, conditionEnd) = TimelineBlockService.CreateConditionBlock();
        conditionStart.ConditionType = MacroConditionType.LoopContext;
        conditionStart.ConditionLoopMode = MacroConditionLoopMode.EveryNRepeats;
        conditionStart.ConditionLoopInterval = 3;
        conditionStart.ConditionShortcutKeys = ConditionInputGesture.Serialize(new[]
        {
            0x11,
            0x12,
            0x51
        });

        timeline.Nodes.Add(conditionStart);
        timeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "inside" });
        timeline.Nodes.Add(conditionEnd);

        MacroStateStore.Save(
            new[] { workspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(3, loadedNodes.Count);
        Assert.Equal(MacroNodeType.ConditionStart, loadedNodes[0].Type);
        Assert.Equal(MacroNodeType.ConditionEnd, loadedNodes[2].Type);
        Assert.Equal(MacroConditionType.LoopContext, loadedNodes[0].ConditionType);
        Assert.Equal(MacroConditionLoopMode.EveryNRepeats, loadedNodes[0].ConditionLoopMode);
        Assert.Equal(3, loadedNodes[0].ConditionLoopInterval);
        Assert.Equal(conditionStart.ConditionShortcutKeys, loadedNodes[0].ConditionShortcutKeys);
        Assert.Equal(loadedNodes[0].ConditionBlockId, loadedNodes[2].ConditionBlockId);
    }

    [Fact]
    public void SaveAndLoad_PreservesNewConditionRunMacroAndToggleSettings()
    {
        var targetWorkspace = CreateWorkspace("Buff Loop", MacroLoopMode.Async);
        targetWorkspace.Id = "macro-b";

        var workspace = CreateWorkspace("Caller", MacroLoopMode.Async);
        workspace.Id = "macro-a";
        var timeline = workspace.Document.ActiveTimeline;

        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.KeyDown,
            KeyName = "Caps Lock",
            VirtualKey = 0x14,
            ToggleKeyMode = ToggleKeyMode.ToggleOff
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.RunMacro,
            RunMacroId = targetWorkspace.Id
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.WindowExists,
            ConditionIsInverted = true,
            WindowReference = new WindowReference
            {
                Type = WindowReferenceType.LastFoundWindow
            }
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.MacroRunning,
            ConditionIsInverted = true,
            ConditionMacroId = targetWorkspace.Id
        });
        timeline.Nodes.Add(new MacroNode
        {
            Type = MacroNodeType.ConditionStart,
            ConditionType = MacroConditionType.TimePassed,
            ConditionIsInverted = true,
            ConditionTimePassedMs = 250
        });

        MacroStateStore.Save(
            new[] { workspace, targetWorkspace },
            0,
            shortcutsEnabled: false,
            new AppSettings());

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        Assert.Equal("macro-a", snapshot.Workspaces[0].Id);
        Assert.Equal("macro-b", snapshot.Workspaces[1].Id);

        var loadedNodes = snapshot.Workspaces[0].Document.ActiveTimeline.Nodes;
        Assert.Equal(ToggleKeyMode.ToggleOff, loadedNodes[0].ToggleKeyMode);
        Assert.Equal(MacroNodeType.RunMacro, loadedNodes[1].Type);
        Assert.Equal("macro-b", loadedNodes[1].RunMacroId);

        Assert.Equal(MacroConditionType.WindowExists, loadedNodes[2].ConditionType);
        Assert.True(loadedNodes[2].ConditionIsInverted);
        Assert.Equal(WindowReferenceType.LastFoundWindow, loadedNodes[2].WindowReference.Type);

        Assert.Equal(MacroConditionType.MacroRunning, loadedNodes[3].ConditionType);
        Assert.True(loadedNodes[3].ConditionIsInverted);
        Assert.Equal("macro-b", loadedNodes[3].ConditionMacroId);

        Assert.Equal(MacroConditionType.TimePassed, loadedNodes[4].ConditionType);
        Assert.False(loadedNodes[4].ConditionIsInverted);
        Assert.Equal(250, loadedNodes[4].ConditionTimePassedMs);
    }

    [Fact]
    public void CloneWorkspace_PreservesHooksAndCooldownAndResetShortcut()
    {
        var source = CreateWorkspace("Hooked", MacroLoopMode.Sequence);
        source.StartHookEnabled = true;
        source.EndHookEnabled = false;
        source.ResetShortcutKeys = "17,82";
        source.StartHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "start" });
        source.EndHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "end" });
        source.Document.ActiveTimeline.CooldownMs = 1500;

        var clone = MacroCloneService.CloneWorkspace(source);

        Assert.Equal(MacroLoopMode.Sequence, clone.LoopMode);
        Assert.True(clone.StartHookEnabled);
        Assert.False(clone.EndHookEnabled);
        Assert.Equal("17,82", clone.ResetShortcutKeys);
        Assert.Equal("start", clone.StartHookTimeline.Nodes[0].Text);
        Assert.Equal("end", clone.EndHookTimeline.Nodes[0].Text);
        Assert.Equal(MacroTimeline.StartHookName, clone.StartHookTimeline.Name);
        Assert.Equal(1500, clone.Document.ActiveTimeline.CooldownMs);
        // Clone is a deep copy.
        Assert.NotSame(source.StartHookTimeline, clone.StartHookTimeline);
    }

    [Fact]
    public void SaveAndLoad_KeepsHooksRegardlessOfEnabledAndPreservesCooldown()
    {
        var workspace = CreateWorkspace("State Hooks", MacroLoopMode.Random);
        workspace.StartHookEnabled = true;
        workspace.EndHookEnabled = false; // disabled but still kept in local state
        workspace.ResetShortcutKeys = "82";
        workspace.StartHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "start-body" });
        workspace.EndHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "end-body" });
        workspace.Document.ActiveTimeline.CooldownMs = 2500;

        MacroStateStore.Save(new[] { workspace }, 0, shortcutsEnabled: false, new AppSettings());
        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loaded = snapshot.Workspaces[0];
        Assert.Equal(MacroLoopMode.Random, loaded.LoopMode);
        Assert.True(loaded.StartHookEnabled);
        Assert.False(loaded.EndHookEnabled);
        Assert.Equal("82", loaded.ResetShortcutKeys);
        Assert.Equal("start-body", loaded.StartHookTimeline.Nodes[0].Text);
        Assert.Equal("end-body", loaded.EndHookTimeline.Nodes[0].Text); // kept even though disabled
        Assert.Equal(2500, loaded.Document.ActiveTimeline.CooldownMs);
    }

    [Fact]
    public void Export_OmitsDisabledHooksButKeepsEnabledHooksAndCooldown()
    {
        var workspace = CreateWorkspace("Export Hooks", MacroLoopMode.Sequence);
        workspace.StartHookEnabled = false; // disabled -> excluded from sharing export
        workspace.EndHookEnabled = true;    // enabled  -> included
        workspace.StartHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "start-secret" });
        workspace.EndHookTimeline.Nodes.Add(new MacroNode { Type = MacroNodeType.Text, Text = "end-shared" });
        workspace.Document.ActiveTimeline.CooldownMs = 750;

        var exportPath = Path.Combine(_appDataRoot, "hooks.keyline");
        Directory.CreateDirectory(_appDataRoot);
        MacroFileStore.Export(exportPath, new[] { workspace });
        var imported = MacroFileStore.Import(exportPath);

        Assert.Single(imported);
        var result = imported[0];
        Assert.False(result.StartHookEnabled);
        Assert.Empty(result.StartHookTimeline.Nodes); // disabled hook content not exported
        Assert.True(result.EndHookEnabled);
        Assert.Equal("end-shared", result.EndHookTimeline.Nodes[0].Text);
        Assert.Equal(750, result.Document.ActiveTimeline.CooldownMs);
    }

    [Fact]
    public void OldFileWithoutHookFields_LoadsWithSafeDefaults()
    {
        Directory.CreateDirectory(MacroStateStore.StateDirectory);
        File.WriteAllText(
            Path.Combine(MacroStateStore.StateDirectory, "state.json"),
            """
            {
              "Version": 3,
              "ActiveWorkspaceIndex": 0,
              "ShortcutsEnabled": false,
              "Settings": {},
              "Workspaces": [
                {
                  "Name": "No Hooks",
                  "ActiveTimelineIndex": 0,
                  "LoopMode": 0,
                  "TimerMs": 0,
                  "BaseDelayMs": 50,
                  "Timelines": [ { "Name": "T1", "LoopCount": 0, "BaseDelayMs": 50, "Nodes": [] } ]
                }
              ]
            }
            """);

        var snapshot = MacroStateStore.Load();

        Assert.NotNull(snapshot);
        var loaded = snapshot.Workspaces[0];
        Assert.False(loaded.StartHookEnabled);
        Assert.False(loaded.EndHookEnabled);
        Assert.Equal(MacroTimeline.StartHookName, loaded.StartHookTimeline.Name);
        Assert.Equal(MacroTimeline.EndHookName, loaded.EndHookTimeline.Name);
        Assert.Equal("", loaded.ResetShortcutKeys);
        Assert.Equal(0, loaded.Document.ActiveTimeline.CooldownMs);
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
