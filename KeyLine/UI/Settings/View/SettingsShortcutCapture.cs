using System.Windows.Controls;
using System.Windows.Input;
using KeyLine.Services.Input;

namespace KeyLine;

public sealed class SettingsShortcutCapture
{
    private readonly ISettingsActions _actions;
    private readonly Action _renderCurrentCategory;
    private readonly Action _notifyChanged;
    private readonly Action _focusOwner;
    private readonly List<int> _capturedShortcutKeys = new();

    private Button? _capturingShortcutButton;
    private Action<string>? _commitShortcut;

    public SettingsShortcutCapture(
        ISettingsActions actions,
        Action renderCurrentCategory,
        Action notifyChanged,
        Action focusOwner)
    {
        _actions = actions;
        _renderCurrentCategory = renderCurrentCategory;
        _notifyChanged = notifyChanged;
        _focusOwner = focusOwner;
    }

    public void Begin(Button button, Action<string> commitShortcut)
    {
        _capturingShortcutButton = button;
        _commitShortcut = commitShortcut;
        _capturedShortcutKeys.Clear();

        button.Content = "press shortcut";
        _actions.SetShortcutCaptureActive(true);
        _focusOwner();
    }

    public bool HandlePreviewKeyDown(KeyEventArgs e)
    {
        if (_capturingShortcutButton == null || _commitShortcut == null)
            return false;

        e.Handled = true;

        if (e.Key is Key.Escape)
        {
            End();
            _renderCurrentCategory();
            return true;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            _commitShortcut("");
            End();
            _renderCurrentCategory();
            _notifyChanged();
            return true;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
        if (virtualKey <= 0 || _capturedShortcutKeys.Contains(virtualKey))
            return true;

        _capturedShortcutKeys.Add(virtualKey);
        _capturingShortcutButton.Content = ShortcutGesture.Format(_capturedShortcutKeys);

        if (_capturedShortcutKeys.Count >= ShortcutGesture.MaxKeyCount)
            Commit();

        return true;
    }

    public bool HandlePreviewKeyUp(KeyEventArgs e)
    {
        if (_capturingShortcutButton == null)
            return false;

        e.Handled = true;
        if (_capturedShortcutKeys.Count > 0)
            Commit();

        return true;
    }

    private void Commit()
    {
        _commitShortcut?.Invoke(ShortcutGesture.Serialize(_capturedShortcutKeys));
        End();
        _renderCurrentCategory();
        _notifyChanged();
    }

    private void End()
    {
        _actions.SetShortcutCaptureActive(false);
        _capturingShortcutButton = null;
        _commitShortcut = null;
        _capturedShortcutKeys.Clear();
    }
}
