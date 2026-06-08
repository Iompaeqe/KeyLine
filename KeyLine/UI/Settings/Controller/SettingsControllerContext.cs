using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Domain;
using KeyLine.Services.Features;

namespace KeyLine;

internal sealed class SettingsControllerContext
{
    private readonly Func<MacroWorkspace> _getActiveWorkspace;
    private readonly Func<string> _getActiveProfileId;
    private readonly Func<IReadOnlyList<MacroWorkspace>> _getActiveProfileWorkspaces;
    private readonly Func<int> _getActiveWorkspaceIndex;
    private readonly Func<bool> _getShortcutsEnabled;
    private readonly Func<double> _getMainWindowWidth;
    private readonly Func<int, AppSettings, MacroWorkspace> _createWorkspace;
    private readonly Action _captureActiveWorkspaceState;
    private readonly Action<int, bool> _activateWorkspace;
    private readonly Action<string> _activateProfile;
    private readonly Action _resetProfiles;
    private readonly Action<bool> _setShortcutsEnabled;
    private readonly Action<double> _applyMainWindowWidth;
    private readonly Action _updateExperimentalAddMenuVisibility;
    private readonly Action _applyShortcutHookState;
    private readonly Action _scheduleSaveState;
    private readonly Action _stopAllRunners;
    private readonly Action _saveStateNow;
    private readonly Action<string> _setStatusText;
    private readonly Action _previewPlaybackSound;

    public SettingsControllerContext(
        Window owner,
        AppSettings settings,
        ContentControl modalHost,
        UIElement modalOverlay,
        IList<MacroWorkspace> workspaces,
        IList<MacroProfile> profiles,
        FeatureGate featureGate,
        Func<MacroWorkspace> getActiveWorkspace,
        Func<string> getActiveProfileId,
        Func<IReadOnlyList<MacroWorkspace>> getActiveProfileWorkspaces,
        Func<int> getActiveWorkspaceIndex,
        Func<bool> getShortcutsEnabled,
        Func<double> getMainWindowWidth,
        Func<int, AppSettings, MacroWorkspace> createWorkspace,
        Action captureActiveWorkspaceState,
        Action<int, bool> activateWorkspace,
        Action<string> activateProfile,
        Action resetProfiles,
        Action<bool> setShortcutsEnabled,
        Action<double> applyMainWindowWidth,
        Action updateExperimentalAddMenuVisibility,
        Action applyShortcutHookState,
        Action scheduleSaveState,
        Action stopAllRunners,
        Action saveStateNow,
        Action<string> setStatusText,
        Action previewPlaybackSound)
    {
        Owner = owner;
        Settings = settings;
        ModalHost = modalHost;
        ModalOverlay = modalOverlay;
        Workspaces = workspaces;
        Profiles = profiles;
        FeatureGate = featureGate;
        _getActiveWorkspace = getActiveWorkspace;
        _getActiveProfileId = getActiveProfileId;
        _getActiveProfileWorkspaces = getActiveProfileWorkspaces;
        _getActiveWorkspaceIndex = getActiveWorkspaceIndex;
        _getShortcutsEnabled = getShortcutsEnabled;
        _getMainWindowWidth = getMainWindowWidth;
        _createWorkspace = createWorkspace;
        _captureActiveWorkspaceState = captureActiveWorkspaceState;
        _activateWorkspace = activateWorkspace;
        _activateProfile = activateProfile;
        _resetProfiles = resetProfiles;
        _setShortcutsEnabled = setShortcutsEnabled;
        _applyMainWindowWidth = applyMainWindowWidth;
        _updateExperimentalAddMenuVisibility = updateExperimentalAddMenuVisibility;
        _applyShortcutHookState = applyShortcutHookState;
        _scheduleSaveState = scheduleSaveState;
        _stopAllRunners = stopAllRunners;
        _saveStateNow = saveStateNow;
        _setStatusText = setStatusText;
        _previewPlaybackSound = previewPlaybackSound;
    }

    public Window Owner { get; }
    public AppSettings Settings { get; }
    public ContentControl ModalHost { get; }
    public UIElement ModalOverlay { get; }
    public IList<MacroWorkspace> Workspaces { get; }
    public IList<MacroProfile> Profiles { get; }
    public FeatureGate FeatureGate { get; }

    public MacroWorkspace GetActiveWorkspace() => _getActiveWorkspace();

    public string GetActiveProfileId() => _getActiveProfileId();

    public IReadOnlyList<MacroWorkspace> GetActiveProfileWorkspaces() => _getActiveProfileWorkspaces();

    public int GetActiveWorkspaceIndex() => _getActiveWorkspaceIndex();

    public bool GetShortcutsEnabled() => _getShortcutsEnabled();

    public double GetMainWindowWidth() => _getMainWindowWidth();

    public MacroWorkspace CreateWorkspace(int index) => _createWorkspace(index, Settings);

    public void CaptureActiveWorkspaceState() => _captureActiveWorkspaceState();

    public void ActivateWorkspace(int index, bool focusTimeline) => _activateWorkspace(index, focusTimeline);

    public void ActivateProfile(string profileId) => _activateProfile(profileId);

    public void ResetProfiles() => _resetProfiles();

    public void SetShortcutsEnabled(bool enabled) => _setShortcutsEnabled(enabled);

    public void ApplyMainWindowWidth(double width) => _applyMainWindowWidth(width);

    public void ScheduleSaveState() => _scheduleSaveState();

    public void StopAllRunners() => _stopAllRunners();

    public void SaveStateNow() => _saveStateNow();

    public void SetStatusText(string text) => _setStatusText(text);

    public void PreviewPlaybackSound() => _previewPlaybackSound();

    public void ApplySettings()
    {
        _updateExperimentalAddMenuVisibility();
        _applyShortcutHookState();
        _scheduleSaveState();
    }
}
