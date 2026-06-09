using System.Windows;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyLine.Domain;
using KeyLine.Interop;
using KeyLine.Services.Features;
using KeyLine.Services.Input;
using KeyLine.Services.Timeline;
using KeyLine.UI;
using KeyLine.UI.Inspector;

namespace KeyLine;

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
                getActiveWorkspace: () => _activeWorkspace,
                getActiveProfileWorkspaces: GetActiveProfileWorkspaces,
                canEdit: () => _isTimelineEditingEnabled,
                saveDocumentUndoSnapshot: SaveDocumentUndoSnapshot,
                refreshTimeline: () => RefreshTimeline(),
                refreshTimelineWithoutInspector: RefreshTimelineWithoutInspector,
                scheduleSaveState: ScheduleSaveState,
                selectTimeline: timeline => SelectTimeline(timeline),
                pickMouseCoordinatesForNodeAsync: PickMouseCoordinatesForNodeAsync,
                pickConditionPixelAsync: PickConditionPixelForNodeAsync);
        }

        private void ToggleRightPanelButton_Click(object sender, RoutedEventArgs e)
        {
            _inspectorDock?.Toggle();
        }

        private void OpenInspectorFromSelection()
        {
            _inspectorDock?.OpenFromSelection();
        }

        private void BeginTimelineNameEditFromHeader(MacroTimeline timeline)
        {
            _inspectorDock?.BeginTimelineNameEdit(timeline);
        }

        private void RefreshInspector()
        {
            _inspectorDock?.Refresh();
        }

        private void RefreshInspectorDeferred()
        {
            Dispatcher.BeginInvoke(new Action(RefreshInspector), DispatcherPriority.Background);
        }

        private void HideInspector()
        {
            _inspectorDock?.Hide();
        }

        private void CloseInspector()
        {
            _inspectorDock?.Close(animate: false);
        }

        private void ShutdownInspector()
        {
            _inspectorDock?.Shutdown();
            _inspectorDock = null;
        }

    // From MainWindow.MacroOptions.cs
        private const string LoopModeAsyncText = "Async";
        private const string LoopModeSyncText = "Sync";
        private const string LoopModeCycleText = "Cycle";
        private const string LoopModeChainText = "Chain";
        private static readonly string[] LoopModeOptions =
        [
            LoopModeAsyncText,
            LoopModeSyncText,
            LoopModeCycleText,
            LoopModeChainText
        ];
        private bool _isUpdatingLoopModeSelection;
        private bool _isUpdatingTimerInput;

        private void InitializeMacroOptions()
        {
            ShortcutBorder.MouseRightButtonDown += ShortcutBorder_MouseRightButtonDown;
            ShortcutPill.MouseLeftButtonDown += ShortcutTextBlock_MouseLeftButtonDown;
            ShortcutPill.PreviewKeyDown += ShortcutTextBlock_PreviewKeyDown;
            ShortcutPill.PreviewKeyUp += ShortcutTextBlock_PreviewKeyUp;
            ShortcutPill.PreviewMouseDown += ShortcutTextBlock_PreviewMouseDown;
            ShortcutPill.PreviewMouseUp += ShortcutTextBlock_PreviewMouseUp;
            ShortcutPill.LostKeyboardFocus += ShortcutTextBlock_LostKeyboardFocus;
            ShortcutTogglePill.MouseLeftButtonDown += ShortcutToggleTextBlock_MouseLeftButtonDown;
            ShortcutRemapPill.MouseLeftButtonDown += ShortcutRemapTextBlock_MouseLeftButtonDown;
            ShortcutRemapPill.Visibility = _featureGate.IsVisible(FeatureId.ShortcutRemap)
                ? Visibility.Visible
                : Visibility.Collapsed;

            LoopModeComboBox.ItemsSource = LoopModeOptions;
            LoopModeComboBox.SelectionChanged += LoopModeComboBox_SelectionChanged;

            InitializeHooksUi();

            TargetWindowSearchPill.MouseLeftButtonDown += TargetWindowSearchPill_MouseLeftButtonDown;
            TargetWindowSearchTextBox.LostFocus += TargetWindowSearchTextBox_LostFocus;
            TargetWindowSearchTextBox.KeyDown += TargetWindowSearchTextBox_KeyDown;
            UpdateAutoWindowFeatureState(isEditable: true);

            TimerMinutesTextBox.TextChanged += TimerMinutesTextBox_TextChanged;
        }

        private void ApplyMacroOptionsFromWorkspace(MacroWorkspace workspace)
        {
            SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, DelayFormatter.ClampMilliseconds(workspace.TimerMs));

            SetLoopModeSelection(workspace.LoopMode);
            ApplyHookOptionsFromWorkspace(workspace);

            TargetWindowSearchTextBox.Text = workspace.TargetWindowSearchName;
            UpdateAutoWindowFeatureState(!IsWorkspaceRunning(workspace));

            ResetShortcutOptionState();
            UpdateShortcutText();
            UpdateShortcutRemapText();
        }

        private void CaptureMacroOptionsToWorkspace(MacroWorkspace workspace)
        {
            workspace.TimerMs = IsWorkspaceRunning(workspace)
                ? GetWorkspaceOriginalTimerMs(workspace)
                : GetTimerMs();

            workspace.LoopMode = GetSelectedMacroLoopMode();
            if (_featureGate.IsEnabled(FeatureId.AutoWindow))
                workspace.TargetWindowSearchName = TargetWindowSearchTextBox.Text.Trim();

            CaptureSelectedTargetWindow(workspace);
        }

        private void SetMacroOptionsEditingEnabled(bool isEnabled)
        {
            LoopModeComboBox.IsEnabled = isEnabled;

            UpdateAutoWindowFeatureState(isEnabled);
            WindowComboBox.IsEnabled = isEnabled;
            HandleComboBox.IsEnabled = isEnabled;
        }

        private MacroLoopMode GetSelectedMacroLoopMode()
        {
            if (LoopModeComboBox.SelectedItem is not string selectedMode)
                return _activeWorkspace.LoopMode;

            if (string.Equals(selectedMode, LoopModeSyncText, StringComparison.OrdinalIgnoreCase))
                return MacroLoopMode.Sync;

            if (string.Equals(selectedMode, LoopModeCycleText, StringComparison.OrdinalIgnoreCase))
                return MacroLoopMode.Cycle;

            if (string.Equals(selectedMode, LoopModeChainText, StringComparison.OrdinalIgnoreCase))
                return MacroLoopMode.Chain;

            return MacroLoopMode.Async;
        }

        private void LoopModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLoopModeSelection)
                return;

            if (!_isTimelineEditingEnabled)
            {
                SetLoopModeSelection(_activeWorkspace.LoopMode);
                return;
            }

            _activeWorkspace.LoopMode = GetSelectedMacroLoopMode();
            CaptureActiveWorkspaceState();
            RefreshTimelineHeaderStatuses();
            ScheduleSaveState();
        }

        private void SetLoopModeSelection(MacroLoopMode loopMode)
        {
            _isUpdatingLoopModeSelection = true;
            try
            {
                LoopModeComboBox.SelectedItem = GetMacroLoopModeText(loopMode);
            }
            finally
            {
                _isUpdatingLoopModeSelection = false;
            }
        }

        private static string GetMacroLoopModeText(MacroLoopMode loopMode)
        {
            return loopMode switch
            {
                MacroLoopMode.Sync => LoopModeSyncText,
                MacroLoopMode.Cycle => LoopModeCycleText,
                MacroLoopMode.Chain => LoopModeChainText,
                _ => LoopModeAsyncText
            };
        }

        private void TimerMinutesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPlaybackCounters || _isUpdatingTimerInput || AnyPlaybackRunning())
                return;

            _activeWorkspace.TimerMs = GetTimerMs();
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
                SetRawDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, DelayFormatter.ClampMilliseconds(_activeWorkspace.TimerMs));
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

            if (ReferenceEquals(sender, TimerMinutesTextBox))
                _activeWorkspace.TimerMs = GetTimerMs();

            FormatDelayInputTextBox(sender);
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private void FormatDelayInputTextBox(object sender)
        {
            if (AnyPlaybackRunning())
                return;

            if (ReferenceEquals(sender, TimerMinutesTextBox))
                SetFormattedDelayInput(TimerMinutesTextBox, TimerUnitTextBlock, DelayFormatter.ClampMilliseconds(_activeWorkspace.TimerMs));
        }

        private void SetFormattedDelayInput(TextBox textBox, TextBlock unitTextBlock, int milliseconds)
        {
            var (value, unit) = DelayFormatter.Split(milliseconds);
            SetDelayInputText(textBox, unitTextBlock, value, unit);
        }

        private void SetRawDelayInput(TextBox textBox, TextBlock unitTextBlock, int milliseconds)
        {
            SetDelayInputText(textBox, unitTextBlock, DelayFormatter.ClampMilliseconds(milliseconds).ToString(), "ms");
        }

        private void SetDelayInputText(TextBox textBox, TextBlock unitTextBlock, string value, string unit)
        {
            _isUpdatingTimerInput = true;
            try
            {
                unitTextBlock.Text = unit;
                textBox.Text = value;
            }
            finally
            {
                _isUpdatingTimerInput = false;
            }
        }

        private int GetTimerMs()
        {
            return ParseDelayInput(TimerMinutesTextBox.Text, TimerUnitTextBlock.Text);
        }

        private static int ParseDelayInput(string valueText, string unitText)
        {
            if (!double.TryParse(valueText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                return string.IsNullOrWhiteSpace(valueText) ? 0 : DelayFormatter.MaxMilliseconds;

            var normalizedUnit = unitText.Trim().ToLowerInvariant();
            var multiplier = normalizedUnit switch
            {
                "s" or "sec" or "secs" or "second" or "seconds" => 1_000,
                "m" or "min" or "mins" or "minute" or "minutes" => 60_000,
                "h" or "hour" or "hours" => 3_600_000,
                _ => 1
            };

            var milliseconds = Math.Round(value * multiplier);
            if (milliseconds >= DelayFormatter.MaxMilliseconds)
                return DelayFormatter.MaxMilliseconds;

            return DelayFormatter.ClampMilliseconds((long)milliseconds);
        }

        private void TargetWindowSearchPill_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TargetWindowSearchPill.IsEnabled)
                return;

            if (!TryUseFeature(FeatureId.AutoWindow))
            {
                e.Handled = true;
                return;
            }

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
            if (!_featureGate.IsEnabled(FeatureId.AutoWindow))
            {
                TargetWindowSearchTextBox.Text = _activeWorkspace.TargetWindowSearchName;
                ShowLockedFeatureStatus(FeatureId.AutoWindow);
                return;
            }

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

        private void UpdateAutoWindowFeatureState(bool isEditable)
        {
            if (_featureGate.IsHidden(FeatureId.AutoWindow))
            {
                TargetWindowSearchPill.Visibility = Visibility.Collapsed;
                TargetWindowSearchPill.IsEnabled = false;
                return;
            }

            TargetWindowSearchPill.Visibility = Visibility.Visible;
            TargetWindowSearchPill.IsEnabled = isEditable;
            TargetWindowSearchPill.Opacity = _featureGate.IsEnabled(FeatureId.AutoWindow) ? 1.0 : 0.55;
            TargetWindowSearchPill.ToolTip = _featureGate.IsEnabled(FeatureId.AutoWindow)
                ? TooltipNotes.TargetWindowSearchName
                : _featureGate.GetLockedFeatureMessage(FeatureId.AutoWindow);
        }

        private void ShortcutToggleTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _activeWorkspace.ShortcutsEnabled = !_activeWorkspace.ShortcutsEnabled;
            ApplyShortcutHookState();
            UpdateShortcutToggleText();
            ScheduleSaveState();
            e.Handled = true;
        }

        private void ShortcutRemapTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (!_featureGate.IsEnabled(FeatureId.ShortcutRemap))
            {
                ShowLockedFeatureStatus(FeatureId.ShortcutRemap);
                return;
            }

            if (IsShortcutCaptureActive())
                CancelShortcutCapture();

            if (_activeWorkspace.ShortcutTriggerBehavior == ShortcutTriggerBehavior.RemapConsume)
            {
                _activeWorkspace.ShortcutTriggerBehavior = ShortcutTriggerBehavior.PassThrough;
                UpdateShortcutRemapText();
                ApplyShortcutHookState();
                ScheduleSaveState();
                return;
            }

            _activeWorkspace.ShortcutTriggerBehavior = ShortcutTriggerBehavior.RemapConsume;
            _activeWorkspace.ShortcutKeys = "";
            _activeWorkspace.ShortcutsEnabled = false;
            ApplyRemapLoopDefault();
            UpdateShortcutText();
            UpdateShortcutRemapText();
            ApplyShortcutHookState();
            ScheduleSaveState();
            SetWarningStatus("Remap mode: loops set to 1; record a single-key shortcut");
            BeginShortcutCapture();
        }

        private void ApplyRemapLoopDefault()
        {
            if (_document.Timelines.Any(timeline => timeline.LoopCount != 1))
                SaveDocumentUndoSnapshot();

            foreach (var timeline in _document.Timelines)
                timeline.LoopCount = 1;

            _activeWorkspace.LoopCount = 1;
            RefreshTimeline();
            RefreshInspector();
        }

        private void ShortcutTextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsShortcutCaptureActive())
            {
                FinishShortcutCaptureWithoutMouseButton();
                e.Handled = true;
                return;
            }

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
            if (IsShortcutCaptureActive())
            {
                FinishShortcutCaptureWithoutMouseButton();
                e.Handled = true;
                return;
            }

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

            if (IsRemapShortcutMode() && !CanCaptureRemapShortcutKey(virtualKey))
            {
                RejectRemapShortcutCapture();
                return;
            }

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

        private void ShortcutTextBlock_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsShortcutCaptureActive())
                return;

            if (IsShortcutCaptureStopMouseButton(e.ChangedButton))
            {
                FinishShortcutCaptureWithoutMouseButton();
                e.Handled = true;
                return;
            }

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);
            if (virtualKey <= 0)
                return;

            e.Handled = true;

            if (IsRemapShortcutMode())
            {
                RejectRemapShortcutCapture();
                return;
            }

            _shortcutCaptureDownKeys.Add(virtualKey);

            if (!_capturedShortcutKeys.Contains(virtualKey) &&
                _capturedShortcutKeys.Count < ShortcutGesture.MaxKeyCount)
            {
                _capturedShortcutKeys.Add(virtualKey);
            }

            UpdateShortcutCaptureText();
        }

        private void ShortcutTextBlock_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsShortcutCaptureActive())
                return;

            var virtualKey = GetVirtualKeyFromMouseButton(e.ChangedButton);
            if (virtualKey <= 0)
                return;

            e.Handled = true;
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

            ShortcutTextBlock.Text = IsRemapShortcutMode() ? "press single key" : "press shortcut";
            ShortcutTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
            ShortcutPill.Focus();
            Mouse.Capture(ShortcutPill);
        }

        private void CommitShortcutCapture(IEnumerable<int> virtualKeys)
        {
            var keys = virtualKeys
                .Select(ShortcutGesture.NormalizeVirtualKey)
                .Where(key => key > 0)
                .Distinct()
                .Take(ShortcutGesture.MaxKeyCount)
                .ToArray();

            if (IsRemapShortcutMode() &&
                keys.Length > 0 &&
                !ShortcutGesture.IsSingleKeyboardKeyShortcut(keys))
            {
                RejectRemapShortcutCapture();
                return;
            }

            if (keys.Any())
            {
                _activeWorkspace.ShortcutKeys = ShortcutGesture.Serialize(keys);

                if (!_activeWorkspace.ShortcutsEnabled)
                {
                    _activeWorkspace.ShortcutsEnabled = true;
                    ApplyShortcutHookState();
                    SuppressCurrentlyHeldShortcutKeys(_activeWorkspace.ShortcutKeys);
                }
                else
                {
                    ApplyShortcutHookState();
                }
            }
            else
            {
                _activeWorkspace.ShortcutKeys = "";
                _activeWorkspace.ShortcutsEnabled = false;
                ApplyShortcutHookState();
            }

            SetShortcutCaptureActive(false);
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();
            if (ReferenceEquals(Mouse.Captured, ShortcutPill))
                Mouse.Capture(null);

            UpdateShortcutText();
            Keyboard.ClearFocus();
            ScheduleSaveState();
        }

        private void CancelShortcutCapture()
        {
            SetShortcutCaptureActive(false);
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();
            if (ReferenceEquals(Mouse.Captured, ShortcutPill))
                Mouse.Capture(null);

            UpdateShortcutText();
            Keyboard.ClearFocus();
        }

        private void FinishShortcutCaptureWithoutMouseButton()
        {
            if (_capturedShortcutKeys.Count > 0)
                CommitShortcutCapture(_capturedShortcutKeys);
            else
                CancelShortcutCapture();
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

            ShortcutToggleTextBlock.Text = _activeWorkspace.ShortcutsEnabled ? "enabled" : "disabled";

            if (_activeWorkspace.ShortcutsEnabled)
            {
                ShortcutToggleTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
            }
            else
            {
                ShortcutToggleTextBlock.ClearValue(ForegroundProperty);
            }
        }

        private void UpdateShortcutRemapText()
        {
            if (ShortcutRemapTextBlock == null)
                return;

            if (!_featureGate.IsVisible(FeatureId.ShortcutRemap))
            {
                ShortcutRemapPill.Visibility = Visibility.Collapsed;
                return;
            }

            var isRemap = _activeWorkspace.ShortcutTriggerBehavior == ShortcutTriggerBehavior.RemapConsume;
            ShortcutRemapPill.Visibility = Visibility.Visible;
            ShortcutRemapTextBlock.Text = isRemap ? "Remap on" : "Remap off";

            if (isRemap)
            {
                ShortcutRemapTextBlock.Foreground = (SolidColorBrush)FindResource("Cyan");
            }
            else
            {
                ShortcutRemapTextBlock.ClearValue(ForegroundProperty);
            }
        }

        private void UpdateShortcutCaptureText()
        {
            ShortcutTextBlock.Text = _capturedShortcutKeys.Count == 0
                ? (IsRemapShortcutMode() ? "press single key" : "press shortcut")
                : ShortcutGesture.Format(_capturedShortcutKeys);
        }

        private bool IsRemapShortcutMode()
        {
            return _activeWorkspace.ShortcutTriggerBehavior == ShortcutTriggerBehavior.RemapConsume;
        }

        private bool CanCaptureRemapShortcutKey(int virtualKey)
        {
            if (!ShortcutGesture.IsKeyboardShortcutKey(virtualKey))
                return false;

            if (Keyboard.Modifiers != ModifierKeys.None)
                return false;

            return _capturedShortcutKeys.Count == 0 ||
                   (_capturedShortcutKeys.Count == 1 &&
                    _capturedShortcutKeys.Contains(ShortcutGesture.NormalizeVirtualKey(virtualKey)));
        }

        private void RejectRemapShortcutCapture()
        {
            _capturedShortcutKeys.Clear();
            _shortcutCaptureDownKeys.Clear();
            _activeWorkspace.ShortcutKeys = "";
            _activeWorkspace.ShortcutsEnabled = false;
            ApplyShortcutHookState();
            UpdateShortcutToggleText();
            ScheduleSaveState();

            ShortcutTextBlock.Text = "single key only";
            ShortcutTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            SetWarningStatus("Remap mode only supports single-key shortcuts.");
        }

        private static int GetVirtualKeyFromKeyEvent(KeyEventArgs e)
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            return ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
        }

        private static int GetVirtualKeyFromMouseButton(MouseButton button)
        {
            return button switch
            {
                MouseButton.XButton1 => NativeMethods.VK_XBUTTON1,
                MouseButton.XButton2 => NativeMethods.VK_XBUTTON2,
                _ => 0
            };
        }

        private static bool IsShortcutCaptureStopMouseButton(MouseButton button) =>
            button is MouseButton.Left or MouseButton.Right or MouseButton.Middle;

    // From MainWindow.TimelineOptions.cs
        private void SelectTimeline(MacroTimeline timeline, bool refreshInspector = true)
        {
            if (_isClearConfirmationActive && !ReferenceEquals(_pendingClearTimeline, timeline))
                ResetClearConfirmation();

            _document.SelectTimeline(timeline);
            if (refreshInspector)
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

