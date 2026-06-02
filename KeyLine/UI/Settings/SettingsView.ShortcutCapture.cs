using System.Windows.Input;
using KeyLine.Services.Input;

namespace KeyLine;

public partial class SettingsView
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_capturingShortcutButton == null || _commitShortcut == null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        if (e.Key is Key.Escape)
        {
            EndShortcutCapture();
            RenderCurrentCategory();
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            _commitShortcut("");
            EndShortcutCapture();
            RenderCurrentCategory();
            NotifyChanged();
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = ShortcutGesture.NormalizeVirtualKey(KeyInterop.VirtualKeyFromKey(key));
        if (virtualKey <= 0 || _capturedShortcutKeys.Contains(virtualKey))
            return;

        _capturedShortcutKeys.Add(virtualKey);
        _capturingShortcutButton.Content = ShortcutGesture.Format(_capturedShortcutKeys);

        if (_capturedShortcutKeys.Count >= ShortcutGesture.MaxKeyCount)
            CommitShortcutCapture();
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_capturingShortcutButton == null)
        {
            base.OnPreviewKeyUp(e);
            return;
        }

        e.Handled = true;
        if (_capturedShortcutKeys.Count > 0)
            CommitShortcutCapture();
    }

    private void CommitShortcutCapture()
    {
        _commitShortcut?.Invoke(ShortcutGesture.Serialize(_capturedShortcutKeys));
        EndShortcutCapture();
        RenderCurrentCategory();
        NotifyChanged();
    }

    private void EndShortcutCapture()
    {
        _actions.SetShortcutCaptureActive(false);
        
        _capturingShortcutButton = null;
        _commitShortcut = null;
        _capturedShortcutKeys.Clear();
    }
}
