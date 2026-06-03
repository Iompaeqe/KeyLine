using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.UI.Tabs;

namespace KeyLine;

public partial class MainWindow
{
    // From MainWindow.Tabs.cs
        private WorkspaceTabController? _workspaceTabs;

        private void InitializeWorkspaceTabs()
        {
            _workspaceTabs = new WorkspaceTabController(
                tabsPanel: MacroTabsPanel,
                scrollViewer: MacroTabsScrollViewer,
                dragOverlay: MacroTabsDragOverlay,
                leftEdgeFade: MacroTabsLeftEdgeFade,
                rightEdgeFade: MacroTabsRightEdgeFade,
                leftEdgeLine: MacroTabsLeftEdgeLine,
                rightEdgeLine: MacroTabsRightEdgeLine,
                dispatcher: Dispatcher,
                getWorkspaces: GetActiveProfileWorkspaces,
                getProfiles: () => _profiles,
                getActiveWorkspaceIndex: GetActiveProfileWorkspaceIndex,
                isWorkspaceRunning: IsWorkspaceRunning,
                activateWorkspace: index =>
                {
                    var workspaceIndex = GetGlobalWorkspaceIndexFromActiveProfileIndex(index);
                    if (workspaceIndex >= 0)
                        ActivateWorkspace(workspaceIndex);
                },
                deleteWorkspace: DeleteWorkspace,
                duplicateWorkspace: DuplicateWorkspace,
                moveWorkspaceToProfile: MoveWorkspaceToProfile,
                reorderWorkspace: ReorderWorkspace,
                showWarning: SetWorkspaceTabWarningStatus,
                scheduleSaveState: ScheduleSaveState,
                setReorderNoticeVisible: MacroTabsBlock.SetReorderNoticeVisible);
        }

        private static MacroWorkspace CreateWorkspace(
            int number,
            AppSettings? settings = null,
            string profileId = MacroProfile.NoProfileId)
        {
            var workspace = new MacroWorkspace
            {
                ProfileId = MacroProfile.NormalizeId(profileId),
                Name = $"Macro {number}",
                TimerMs = settings?.DefaultTimerMs ?? 0,
                LoopCount = settings?.DefaultLoopCount ?? 0,
                BaseDelayMs = settings?.DefaultBaseDelayMs ?? 50,
                LoopMode = settings?.DefaultLoopMode ?? MacroLoopMode.Async
            };

            if (settings != null)
                ApplyDefaultSettingsToTimeline(workspace.Document.ActiveTimeline, settings);

            return workspace;
        }

        private static void ApplyDefaultSettingsToTimeline(MacroTimeline timeline, AppSettings settings)
        {
            timeline.UseStandardDelay = settings.DefaultStandardDelayEnabled;
            timeline.StandardDelayMs = Math.Max(0, settings.DefaultStandardDelayMs);
            timeline.ShowKeyUpDown = !settings.DefaultStandardDelayEnabled || settings.DefaultShowKeyUpDown;
            timeline.UseTextInputMode = settings.DefaultTextInputMode;
            timeline.LoopCount = Math.Max(0, settings.DefaultLoopCount);
            timeline.BaseDelayMs = Math.Max(0, settings.DefaultBaseDelayMs);
        }

        private int GetNextWorkspaceNumber(string profileId)
        {
            var usedNumbers = new HashSet<int>();

            foreach (var workspace in GetWorkspacesForProfile(profileId))
            {
                if (!TryParseDefaultWorkspaceNumber(workspace.Name, out var number))
                    continue;

                usedNumbers.Add(number);
            }

            var candidate = 1;
            while (usedNumbers.Contains(candidate))
                candidate++;

            return candidate;
        }

        private static bool TryParseDefaultWorkspaceNumber(string name, out int number)
        {
            number = 0;

            const string prefix = "Macro ";
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(name[prefix.Length..], out number) && number > 0;
        }

        private void AddMacroTabButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CaptureActiveWorkspaceState();

            var workspace = CreateWorkspace(GetNextWorkspaceNumber(_activeProfileId), _settings, _activeProfileId);
            _workspaces.Add(workspace);
            ActivateWorkspace(_workspaces.IndexOf(workspace));
        }

        private void ActivateWorkspace(int index, bool saveCurrent = true)
        {
            if (index < 0 || index >= _workspaces.Count)
                return;

            _workspaceTabs?.ClearTransientState();

            if (saveCurrent)
                CaptureActiveWorkspaceState();

            if (_recorder.IsRecording)
                StopRecording();

            _isSwitchingWorkspace = true;

            try
            {
                _activeWorkspaceIndex = index;
                _activeWorkspace = _workspaces[_activeWorkspaceIndex];
                _activeProfileId = MacroProfile.NormalizeId(_activeWorkspace.ProfileId);
                _document = _activeWorkspace.Document;

                _selection.Clear();
                _timelineVisualPositions.Clear();
                ResetClearConfirmation();

                ApplyMacroOptionsFromWorkspace(_activeWorkspace);

                RefreshMacroTabs();
                RefreshProfileDropdown();
                RestoreTargetWindowSelection(_activeWorkspace);
                if (!HasResolvedTargetSelection() && !string.IsNullOrWhiteSpace(_activeWorkspace.TargetWindowSearchName))
                    TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);
                SelectTimeline(_document.ActiveTimeline);
                RefreshTimeline();
                RefreshActiveWorkspacePlaybackUi();
                ApplyShortcutHookState();
            }
            finally
            {
                _isSwitchingWorkspace = false;
            }

            if (saveCurrent)
                ScheduleSaveState();
        }

        private void RefreshMacroTabs()
        {
            _workspaceTabs?.Refresh();
        }

        private void CaptureActiveWorkspaceState()
        {
            if (_isSwitchingWorkspace)
                return;

            _activeWorkspace.Document = _document;

            CaptureMacroOptionsToWorkspace(_activeWorkspace);

            _activeWorkspace.LoopCount = _document.ActiveTimeline.LoopCount;
            _activeWorkspace.BaseDelayMs = _document.ActiveTimeline.BaseDelayMs;
        }

    // From MainWindow.TabEditing.cs
        private void DeleteWorkspace(MacroWorkspace workspace)
        {
            var profileWorkspaces = GetWorkspacesForProfile(workspace.ProfileId);
            if (profileWorkspaces.Count <= 1)
                return;

            var index = _workspaces.IndexOf(workspace);
            if (index < 0)
                return;

            if (IsWorkspaceRunning(workspace))
            {
                RefreshMacroTabs();
                SetWorkspaceTabWarningStatus("Stop this macro before deleting it");
                return;
            }

            if (_recorder.IsRecording)
                StopRecording();

            var activeWorkspace = _activeWorkspace;
            _workspaces.RemoveAt(index);

            var nextWorkspaceIndex = ReferenceEquals(workspace, activeWorkspace)
                ? IndexOfFirstWorkspaceInProfile(_activeProfileId)
                : _workspaces.IndexOf(activeWorkspace);

            if (nextWorkspaceIndex < 0)
                nextWorkspaceIndex = Math.Clamp(_activeWorkspaceIndex, 0, _workspaces.Count - 1);

            ActivateWorkspace(nextWorkspaceIndex, false);
            ScheduleSaveState();
        }

        private void DuplicateWorkspace(MacroWorkspace workspace)
        {
            DuplicateWorkspace(_workspaces.IndexOf(workspace));
        }

        private void MoveWorkspaceToProfile(MacroWorkspace workspace, string profileId)
        {
            if (!_workspaces.Contains(workspace))
                return;

            profileId = MacroProfile.NormalizeId(profileId);
            if (string.Equals(
                    MacroProfile.NormalizeId(workspace.ProfileId),
                    profileId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CaptureActiveWorkspaceState();

            var sourceProfileId = MacroProfile.NormalizeId(workspace.ProfileId);
            var wasActiveWorkspace = ReferenceEquals(workspace, _activeWorkspace);
            workspace.ProfileId = profileId;
            workspace.Name = WorkspaceNameService.GetUniqueName(
                GetWorkspacesForProfile(profileId).Where(existing => !ReferenceEquals(existing, workspace)),
                workspace.Name,
                "Moved Macro");

            if (GetWorkspacesForProfile(sourceProfileId).Count == 0)
                _workspaces.Add(CreateWorkspace(GetNextWorkspaceNumber(sourceProfileId), _settings, sourceProfileId));

            if (wasActiveWorkspace)
            {
                var nextWorkspaceIndex = IndexOfFirstWorkspaceInProfile(sourceProfileId);
                if (nextWorkspaceIndex >= 0)
                    ActivateWorkspace(nextWorkspaceIndex, saveCurrent: false);
            }
            else
            {
                RefreshMacroTabs();
                RefreshProfileDropdown();
            }

            ScheduleSaveState();
        }

        private void ReorderWorkspace(int sourceIndex, int targetIndex)
        {
            var profileWorkspaces = GetActiveProfileWorkspaces().ToList();

            if (sourceIndex < 0 || sourceIndex >= profileWorkspaces.Count)
                return;

            if (targetIndex < 0 || targetIndex >= profileWorkspaces.Count || sourceIndex == targetIndex)
                return;

            CaptureActiveWorkspaceState();

            var activeWorkspace = _activeWorkspace;
            var reorderedProfileWorkspaces = profileWorkspaces;
            var workspace = reorderedProfileWorkspaces[sourceIndex];
            reorderedProfileWorkspaces.RemoveAt(sourceIndex);
            reorderedProfileWorkspaces.Insert(targetIndex, workspace);

            var profileIndex = 0;
            for (var i = 0; i < _workspaces.Count; i++)
            {
                if (!IsWorkspaceInProfile(_workspaces[i], _activeProfileId))
                    continue;

                _workspaces[i] = reorderedProfileWorkspaces[profileIndex++];
            }

            _activeWorkspaceIndex = _workspaces.IndexOf(activeWorkspace);
            if (_activeWorkspaceIndex < 0)
                _activeWorkspaceIndex = Math.Clamp(targetIndex, 0, _workspaces.Count - 1);

            _activeWorkspace = _workspaces[_activeWorkspaceIndex];
            _document = _activeWorkspace.Document;

            RefreshMacroTabs();
            ScheduleSaveState();
        }

        private void SetWorkspaceTabWarningStatus(string text)
        {
            StatusText.Text = text;
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
        }

    // From MainWindow.TabScrolling.cs
        private void MacroTabsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _workspaceTabs?.PreviewMouseWheel(e);
        }

        private void MacroTabsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _workspaceTabs?.ScrollChanged();
        }

        private void MacroTabsScrollViewer_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            _workspaceTabs?.SizeChanged();
        }

        private void MacroTabsScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _workspaceTabs?.PreviewMouseLeftButtonDown(e);
        }

        private void MacroTabsScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            _workspaceTabs?.PreviewMouseMove(e);
        }

        private void MacroTabsScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _workspaceTabs?.PreviewMouseLeftButtonUp();
        }

        private void MacroTabsScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            _workspaceTabs?.MouseLeave(e);
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_workspaceTabs?.IsReorderModeEnabled != true)
                return;

            if (IsSourceInsideMacroTabsScrollArea(e.OriginalSource as DependencyObject))
                return;

            _workspaceTabs.DisableReorderMode();
        }

        private bool IsSourceInsideMacroTabsScrollArea(DependencyObject? source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, MacroTabsScrollViewer))
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void UpdateMacroTabEdgeIndicators()
        {
            _workspaceTabs?.ScrollChanged();
        }

}

