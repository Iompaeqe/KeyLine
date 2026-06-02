using System;
using System.Collections.Generic;
using System.Windows;
using MacroSpammer.Domain;
using MacroSpammer.UI.Tabs;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;

namespace MacroSpammer;

public partial class MainWindow
{
    // From MainWindow.Tabs.cs
        private WorkspaceTabController? _workspaceTabs;

        private void InitializeWorkspaceTabs()
        {
            _workspaceTabs = new WorkspaceTabController(
                tabsPanel: MacroTabsPanel,
                scrollViewer: MacroTabsScrollViewer,
                leftEdgeFade: MacroTabsLeftEdgeFade,
                rightEdgeFade: MacroTabsRightEdgeFade,
                leftEdgeLine: MacroTabsLeftEdgeLine,
                rightEdgeLine: MacroTabsRightEdgeLine,
                dispatcher: Dispatcher,
                getWorkspaces: () => _workspaces,
                getActiveWorkspaceIndex: () => _activeWorkspaceIndex,
                isWorkspaceRunning: IsWorkspaceRunning,
                activateWorkspace: index => ActivateWorkspace(index),
                deleteWorkspace: DeleteWorkspace,
                showWarning: SetWorkspaceTabWarningStatus,
                scheduleSaveState: ScheduleSaveState);
        }

        private static MacroWorkspace CreateWorkspace(int number, AppSettings? settings = null)
        {
            var workspace = new MacroWorkspace
            {
                Name = $"Macro {number}",
                TimerMs = settings?.DefaultTimerMs ?? 0,
                LoopCount = settings?.DefaultLoopCount ?? 0,
                BaseDelayMs = settings?.DefaultBaseDelayMs ?? 50,
                LoopType = settings?.DefaultLoopType ?? MacroLoopType.Async
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

        private int GetNextWorkspaceNumber()
        {
            var usedNumbers = new HashSet<int>();

            foreach (var workspace in _workspaces)
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

            var workspace = CreateWorkspace(GetNextWorkspaceNumber(), _settings);
            _workspaces.Add(workspace);
            ActivateWorkspace(_workspaces.Count - 1);
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
                _document = _activeWorkspace.Document;

                _selection.Clear();
                _timelineVisualPositions.Clear();
                ResetClearConfirmation();

                ApplyMacroOptionsFromWorkspace(_activeWorkspace);

                RefreshMacroTabs();
                RestoreTargetWindowSelection(_activeWorkspace);
                if (!HasResolvedTargetSelection() && !string.IsNullOrWhiteSpace(_activeWorkspace.TargetWindowSearchName))
                    TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);
                SelectTimeline(_document.ActiveTimeline);
                RefreshTimeline();
                RefreshActiveWorkspacePlaybackUi();
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
            if (_workspaces.Count <= 1)
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

            _workspaces.RemoveAt(index);

            if (_activeWorkspaceIndex > index)
                _activeWorkspaceIndex--;
            else if (_activeWorkspaceIndex >= _workspaces.Count)
                _activeWorkspaceIndex = _workspaces.Count - 1;

            ActivateWorkspace(_activeWorkspaceIndex, false);
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

        private void UpdateMacroTabEdgeIndicators()
        {
            _workspaceTabs?.ScrollChanged();
        }

}
