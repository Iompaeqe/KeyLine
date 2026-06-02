using System.Windows;
using MacroSpammer.UI.Inspector;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacroSpammer.Domain;
using MacroSpammer.Services.Input;
using MacroSpammer.Services.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    // From MainWindow.Inspector.cs
        private InspectorDockController? _inspectorDock;

        private void InitializeInspector()
        {
            _inspectorDock = new InspectorDockController(
                owner: this,
                selection: _selection,
                getCurrentTimeline: () => _selection.SelectedTimeline ?? _document.ActiveTimeline,
                canEdit: () => _isTimelineEditingEnabled,
                saveUndoSnapshot: SaveUndoSnapshot,
                refreshTimeline: () => RefreshTimeline(),
                scheduleSaveState: ScheduleSaveState,
                selectTimeline: SelectTimeline,
                pickMouseCoordinatesForNodeAsync: PickMouseCoordinatesForNodeAsync);
        }

        private void ToggleRightPanelButton_Click(object sender, RoutedEventArgs e)
        {
            _inspectorDock?.Toggle();
        }

        private void OpenInspectorFromSelection()
        {
            _inspectorDock?.OpenFromSelection();
        }

        private void RefreshInspector()
        {
            _inspectorDock?.Refresh();
        }

        private void HideInspector()
        {
            _inspectorDock?.Hide();
        }

        private void ShutdownInspector()
        {
            _inspectorDock?.Shutdown();
            _inspectorDock = null;
        }

    // From MainWindow.MacroOptions.cs
        private const string LoopTypeAsyncText = "asynced";
        private const string LoopTypeSyncText = "synced";

        private void InitializeMacroOptions()
        {
            ShortcutBorder.MouseRightButtonDown += ShortcutBorder_MouseRightButtonDown;
            ShortcutPill.MouseLeftButtonDown += ShortcutTextBlock_MouseLeftButtonDown;
            ShortcutPill.PreviewKeyDown += ShortcutTextBlock_PreviewKeyDown;
            ShortcutPill.PreviewKeyUp += ShortcutTextBlock_PreviewKeyUp;
            ShortcutPill.LostKeyboardFocus += ShortcutTextBlock_LostKeyboardFocus;
            ShortcutTogglePill.MouseLeftButtonDown += ShortcutToggleTextBlock_MouseLeftButtonDown;

            LoopTypePager.PageRequested += LoopTypePager_PageRequested;

            TargetWindowSearchPill.MouseLeftButtonDown += TargetWindowSearchPill_MouseLeftButtonDown;
            TargetWindowSearchTextBox.LostFocus += TargetWindowSearchTextBox_LostFocus;
            TargetWindowSearchTextBox.KeyDown += TargetWindowSearchTextBox_KeyDown;

            TimerMinutesTextBox.TextChanged += TimerMinutesTextBox_TextChanged;
        }

        private void ApplyMacroOptionsFromWorkspace(MacroWorkspace workspace)
        {
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, Math.Max(0, workspace.TimerMs));

            LoopTypePager.Text = workspace.LoopType == MacroLoopType.Sync
                ? LoopTypeSyncText
                : LoopTypeAsyncText;

            TargetWindowSearchTextBox.Text = workspace.TargetWindowSearchName;

            ResetShortcutOptionState();
            UpdateShortcutText();
        }

        private void CaptureMacroOptionsToWorkspace(MacroWorkspace workspace)
        {
            workspace.TimerMs = IsWorkspaceRunning(workspace)
                ? Math.Max(0, _originalTimerMs)
                : GetTimerMs();

            workspace.LoopType = GetSelectedMacroLoopType();
            workspace.TargetWindowSearchName = TargetWindowSearchTextBox.Text.Trim();

            CaptureSelectedTargetWindow(workspace);
        }

        private void SetMacroOptionsEditingEnabled(bool isEnabled)
        {
            LoopTypePager.IsEnabled = isEnabled;

            TargetWindowSearchPill.IsEnabled = isEnabled;
            WindowComboBox.IsEnabled = isEnabled;
            HandleComboBox.IsEnabled = isEnabled;
        }

        private MacroLoopType GetSelectedMacroLoopType()
        {
            return string.Equals(LoopTypePager.Text, LoopTypeSyncText, StringComparison.OrdinalIgnoreCase)
                ? MacroLoopType.Sync
                : MacroLoopType.Async;
        }

        private void LoopTypePager_PageRequested(object? sender, EventArgs e)
        {
            if (!_isTimelineEditingEnabled)
                return;

            _activeWorkspace.LoopType = _activeWorkspace.LoopType == MacroLoopType.Sync
                ? MacroLoopType.Async
                : MacroLoopType.Sync;

            LoopTypePager.Text = _activeWorkspace.LoopType == MacroLoopType.Sync
                ? LoopTypeSyncText
                : LoopTypeAsyncText;

            CaptureActiveWorkspaceState();
            ScheduleSaveState();
        }

        private void TimerMinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdatingPlaybackCounters)
                ScheduleSaveState();
        }

        private void DelayInputTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void DelayInputTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (AnyPlaybackRunning())
                return;

            if (ReferenceEquals(sender, TimerMinutesTextBox))
            {
                var timerMs = GetTimerMs();
                TimerUnitTextBlock.Text = "ms";
                TimerMinutesTextBox.Text = timerMs.ToString();
            }

            if (sender is TextBox textBox)
                textBox.SelectAll();
        }

        private void DelayInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            FormatDelayInputTextBox(sender);
        }

        private void DelayInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            FormatDelayInputTextBox(sender);
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private void FormatDelayInputTextBox(object sender)
        {
            if (AnyPlaybackRunning())
                return;

            if (ReferenceEquals(sender, TimerMinutesTextBox))
                SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, GetTimerMs());
        }

        private static void SetFormattedDelayInput(TextBox textBox, TextBlock unitTextBlock, int milliseconds)
        {
            var (value, unit) = DelayFormatter.Split(milliseconds);
            textBox.Text = value;
            unitTextBlock.Text = unit;
        }

        private int GetTimerMs()
        {
            return ParseDelayInput(TimerMinutesTextBox.Text, TimerUnitTextBlock.Text);
        }

        private static int ParseDelayInput(string valueText, string unitText)
        {
            if (!double.TryParse(valueText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return 0;

            var multiplier = unitText switch
            {
                "sec" => 1_000,
                "min" => 60_000,
                "hours" => 3_600_000,
                _ => 1
            };

            return Math.Max(0, (int)Math.Round(value * multiplier));
        }

        private void TargetWindowSearchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TargetWindowSearchPill.IsEnabled)
                return;

            TargetWindowSearchPill.IsTextInput = true;
            TargetWindowSearchPill.FocusInput();
            e.Handled = true;
        }

        private void TargetWindowSearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitTargetWindowSearchName(resolveIfMissingTarget: true);
            TargetWindowSearchPill.IsTextInput = false;
        }

        private void TargetWindowSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    CommitTargetWindowSearchName(resolveIfMissingTarget: true);
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    TargetWindowSearchTextBox.Text = _activeWorkspace.TargetWindowSearchName;
                    TargetWindowSearchPill.IsTextInput = false;
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    break;
            }
        }

        private void CommitTargetWindowSearchName(bool resolveIfMissingTarget)
        {
            var searchName = TargetWindowSearchTextBox.Text.Trim();

            if (string.Equals(_activeWorkspace.TargetWindowSearchName, searchName, StringComparison.Ordinal))
            {
                if (resolveIfMissingTarget && !string.IsNullOrWhiteSpace(searchName))
                    TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);

                return;
            }

            _activeWorkspace.TargetWindowSearchName = searchName;
            ClearMacroError(_activeWorkspace);

            if (resolveIfMissingTarget && !string.IsNullOrWhiteSpace(searchName))
                TryResolveTargetWindowSearchName(_activeWorkspace, updateSelection: true);

            ScheduleSaveState();
        }

        private void ShortcutToggleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _shortcutsEnabled = !_shortcutsEnabled;
            ApplyShortcutHookState();
            UpdateShortcutToggleText();
            ScheduleSaveState();
            e.Handled = true;
        }

        private void ShortcutTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isShortcutClearConfirmationActive)
            {
                CommitShortcutCapture(Array.Empty<int>());
                ResetShortcutClearConfirmation();
                e.Handled = true;
                return;
            }

            BeginShortcutCapture();
            e.Handled = true;
        }

        private void ShortcutBorder_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_activeWorkspace.ShortcutKeys))
                return;

            if (_isShortcutClearConfirmationActive)
            {
                CommitShortcutCapture(Array.Empty<int>());
                ResetShortcutClearConfirmation();
            }
            else
            {
                BeginShortcutClearConfirmation();
            }

            e.Handled = true;
        }

        private void BeginShortcutClearConfirmation()
        {
            _isShortcutClearConfirmationActive = true;
            CancelShortcutCapture();

            ShortcutTextBlock.Text = "clear? confirm";
            ShortcutTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
            ShortcutBorder.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            ShortcutBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }

        private void ResetShortcutClearConfirmation()
        {
            _isShortcutClearConfirmationActive = false;
            ShortcutBorder.ClearValue(BackgroundProperty);
            ShortcutBorder.ClearValue(BorderBrushProperty);
            UpdateShortcutText();
        }

        private void ShortcutTextBlock_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsShortcutCaptureActive())
                return;

            e.Handled = true;

            if (e.Key == Key.Escape)
            {
                CancelShortcutCapture();
                return;
            }

            if (e.Key is Key.Back or Key.Delete)
            {
                CommitShortcutCapture(Array.Empty<int>());
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitShortcutCapture(_capturedShortcutKeys);
                return;
            }

            var virtualKey = GetVirtualKeyFromKeyEvent(e);
            if (virtualKey <= 0)
                return;

            _shortcutCaptureDownKeys.Add(virtualKey);

            if (!_capturedShortcutKeys.Contains(virtualKey) &&
                _capturedShortcutKeys.Count < ShortcutGesture.MaxKeyCount)
            {
                _capturedShortcutKeys.Add(virtualKey);
            }

            UpdateShortcutCaptureText();
        }

        private void ShortcutTextBlock_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (!IsShortcutCaptureActive())
                return;

            e.Handled = true;

            var virtualKey = GetVirtualKeyFromKeyEvent(e);
            if (virtualKey > 0)
                _shortcutCaptureDownKeys.Remove(virtualKey);

            if (_capturedShortcutKeys.Count > 0 && _shortcutCaptureDownKeys.Count == 0)
                CommitShortcutCapture(_capturedShortcutKeys);
        }

        private void ShortcutTextBlock_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_isShortcutClearConfirmationActive)
            {
                ResetShortcutClearConfirmation();
                return;
            }

            if (!IsShortcutCaptureActive())
                return;

            if (_capturedShortcutKeys.Count > 0)
                CommitShortcutCapture(_capturedShortcutKeys);
            else
                CancelShortcutCapture();
        }

        private void BeginShortcutCapture()
        {
            ResetShortcutClearConfirmation();

            SetShortcutCaptureActive(true);
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();

            ShortcutTextBlock.Text = "press shortcut";
            ShortcutTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
            ShortcutPill.Focus();
        }

        private void CommitShortcutCapture(IEnumerable<int> virtualKeys)
        {
            if (virtualKeys.Any())
            {
                _activeWorkspace.ShortcutKeys = ShortcutGesture.Serialize(virtualKeys);

                if (!_shortcutsEnabled)
                {
                    _shortcutsEnabled = true;
                    ApplyShortcutHookState();
                    SuppressCurrentlyHeldShortcutKeys(_activeWorkspace.ShortcutKeys);
                }
            }
            else
            {
                _activeWorkspace.ShortcutKeys = "";
                _shortcutsEnabled = false;
                ApplyShortcutHookState();
            }

            SetShortcutCaptureActive(false);
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();

            UpdateShortcutText();
            Keyboard.ClearFocus();
            ScheduleSaveState();
        }

        private void CancelShortcutCapture()
        {
            SetShortcutCaptureActive(false);
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();

            UpdateShortcutText();
            Keyboard.ClearFocus();
        }

        private void ResetShortcutOptionState()
        {
            SetShortcutCaptureActive(false);
            _isShortcutClearConfirmationActive = false;
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();

            ShortcutBorder.ClearValue(BackgroundProperty);
            ShortcutBorder.ClearValue(BorderBrushProperty);
        }

        private void UpdateShortcutText()
        {
            if (ShortcutTextBlock == null)
                return;

            ShortcutTextBlock.Text = ShortcutGesture.Format(_activeWorkspace.ShortcutKeys);
            ShortcutTextBlock.ClearValue(ForegroundProperty);

            UpdateShortcutToggleText();
        }

        private void UpdateShortcutToggleText()
        {
            if (ShortcutToggleTextBlock == null)
                return;

            ShortcutToggleTextBlock.Text = _shortcutsEnabled ? "on" : "off";

            if (_shortcutsEnabled)
            {
                ShortcutToggleTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
            }
            else
            {
                ShortcutToggleTextBlock.ClearValue(ForegroundProperty);
            }
        }

        private void UpdateShortcutCaptureText()
        {
            ShortcutTextBlock.Text = _capturedShortcutKeys.Count == 0
                ? "press shortcut"
                : ShortcutGesture.Format(_capturedShortcutKeys);
        }

        private static int GetVirtualKeyFromKeyEvent(KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            return ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
        }

    // From MainWindow.TimelineOptions.cs
        private void SelectTimeline(MacroTimeline timeline)
        {
            if (_isClearConfirmationActive && !ReferenceEquals(_pendingClearTimeline, timeline))
                ResetClearConfirmation();

            _document.SelectTimeline(timeline);
            SyncOptionsFromActiveTimeline();
            ScheduleSaveState();
        }

        private void SyncOptionsFromActiveTimeline()
        {
            RefreshInspector();
        }

        private void UpdateTimelineOptionsPagerVisibility()
        {
        }

}
