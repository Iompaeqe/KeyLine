using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MacroSpammer.Interop;
using MacroSpammer.UI;

namespace MacroSpammer.Services.Input;

public sealed class MouseCoordinatePicker
{
    private NativeMethods.LowLevelMouseProc? _mousePickProc;
    private IntPtr _mousePickHook;
    private MouseTargetIndicatorWindow? _mouseTargetIndicator;

    public async Task<Point?> PickAsync(Window owner, nint targetHwnd)
    {
        if (targetHwnd == 0)
            return null;

        ShowMouseTargetIndicator(owner, targetHwnd);

        try
        {
            NativeMethods.SetForegroundWindow(targetHwnd);
            return await CaptureNextMousePointAsync(targetHwnd);
        }
        finally
        {
            CloseMouseTargetIndicator();
            BringOwnerToFront(owner);
        }
    }

    private void ShowMouseTargetIndicator(Window owner, nint targetHwnd)
    {
        CloseMouseTargetIndicator();

        _mouseTargetIndicator = new MouseTargetIndicatorWindow
        {
            Owner = owner
        };

        PositionMouseTargetIndicator(_mouseTargetIndicator, targetHwnd);
        _mouseTargetIndicator.Show();
    }

    private static void PositionMouseTargetIndicator(Window indicator, nint targetHwnd)
    {
        if (!NativeMethods.GetWindowRect(targetHwnd, out var rect))
        {
            indicator.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var width = rect.Right - rect.Left;
        var left = rect.Left + Math.Max(12, (width - indicator.Width) / 2.0);
        var top = rect.Top + 18;

        indicator.Left = left;
        indicator.Top = top;
    }

    private void CloseMouseTargetIndicator()
    {
        _mouseTargetIndicator?.Close();
        _mouseTargetIndicator = null;
    }

    private static void BringOwnerToFront(Window owner)
    {
        var hwnd = new WindowInteropHelper(owner).Handle;
        NativeMethods.SetForegroundWindow(hwnd);

        if (owner.WindowState == WindowState.Minimized)
            owner.WindowState = WindowState.Normal;

        owner.Activate();
        owner.Topmost = true;
        owner.Topmost = false;
    }

    private async Task<Point?> CaptureNextMousePointAsync(nint targetHwnd)
    {
        var completion = new TaskCompletionSource<Point?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var moduleHandle = NativeMethods.GetModuleHandle(null);

        _mousePickProc = (code, wParam, lParam) =>
        {
            if (code >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
            {
                var hookData = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var point = hookData.pt;

                if (NativeMethods.ScreenToClient(targetHwnd, ref point))
                    completion.TrySetResult(new Point(point.X, point.Y));
                else
                    completion.TrySetResult(null);
            }

            return NativeMethods.CallNextHookEx(_mousePickHook, code, wParam, lParam);
        };

        _mousePickHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _mousePickProc,
            moduleHandle,
            0);

        if (_mousePickHook == IntPtr.Zero)
        {
            _mousePickProc = null;
            return null;
        }

        try
        {
            var completedTask = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));

            return completedTask == completion.Task
                ? await completion.Task
                : null;
        }
        finally
        {
            if (_mousePickHook != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_mousePickHook);
                _mousePickHook = IntPtr.Zero;
            }

            _mousePickProc = null;
        }
    }
}
