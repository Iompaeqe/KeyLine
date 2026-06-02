using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace MacroSpammer.Services.Tray;

public sealed class TrayController : IDisposable
{
    private readonly Window _owner;
    private readonly string _text;
    private readonly Action _beforeHide;

    private Forms.NotifyIcon? _trayIcon;
    private bool _isClosingForExit;

    public TrayController(Window owner, string text, Action beforeHide)
    {
        _owner = owner;
        _text = text;
        _beforeHide = beforeHide;
    }

    public void HideToTray()
    {
        _beforeHide();
        EnsureTrayIcon();
        _owner.Hide();
    }

    public void ShowFromTray()
    {
        _owner.Show();
        _owner.WindowState = WindowState.Normal;
        _owner.Activate();
    }

    public bool ShouldCancelClose(bool closeToTray)
    {
        if (_isClosingForExit || !closeToTray)
            return false;

        HideToTray();
        return true;
    }

    public void EnsureTrayIcon()
    {
        if (_trayIcon != null)
            return;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowFromTray());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _isClosingForExit = true;
            _owner.Close();
        });

        _trayIcon = new Forms.NotifyIcon
        {
            Text = _text,
            Visible = true,
            ContextMenuStrip = menu,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
        };
        _trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    public void Dispose()
    {
        if (_trayIcon == null)
            return;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
    }
}