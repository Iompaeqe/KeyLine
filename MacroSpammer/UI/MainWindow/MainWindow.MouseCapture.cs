using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MacroSpammer.Domain;
using MacroSpammer.Interop;
using MacroSpammer.UI;

namespace MacroSpammer;

public partial class MainWindow
{
    private NativeMethods.LowLevelMouseProc? _mousePickProc;
    private IntPtr _mousePickHook;
    private MouseTargetIndicatorWindow? _mouseTargetIndicator;

    private async Task PickMouseCoordinatesForStepAsync(MacroStep step)
    {
        var target = GetTargetHandle();
        if (target == null || target.Handle == 0)
        {
            StatusText.Text = "Select a target window before picking mouse coordinates";
            return;
        }

        var previousStatus = StatusText.Text;
        StatusText.Text = "Click inside the target window to capture X/Y";

        ShowMouseTargetIndicator(target.Handle);
        var picked = await CaptureNextMousePointAsync(target.Handle);
        CloseMouseTargetIndicator();
        BringSpammerToFront();

        if (picked == null)
        {
            StatusText.Text = previousStatus;
            return;
        }

        step.MouseX = Math.Max(0, (int)picked.Value.X);
        step.MouseY = Math.Max(0, (int)picked.Value.Y);

        StatusText.Text = $"Captured mouse point ({step.MouseX}, {step.MouseY})";
        RefreshTimeline();
        ScheduleSaveState();
    }

    private void ShowMouseTargetIndicator(nint targetHwnd)
    {
        CloseMouseTargetIndicator();

        _mouseTargetIndicator = new MouseTargetIndicatorWindow
        {
            Owner = this
        };

        PositionMouseTargetIndicator(_mouseTargetIndicator, targetHwnd);
        _mouseTargetIndicator.Show();
    }

    private void PositionMouseTargetIndicator(Window indicator, nint targetHwnd)
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

    private void BringSpammerToFront()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetForegroundWindow(hwnd);

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Topmost = true;
        Topmost = false;
    }

    private async Task<Point?> CaptureNextMousePointAsync(nint targetHwnd)
    {
        var completion = new TaskCompletionSource<Point?>();
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

        NativeMethods.SetForegroundWindow(targetHwnd);

        var completedTask = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));

        if (_mousePickHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mousePickHook);
            _mousePickHook = IntPtr.Zero;
        }

        _mousePickProc = null;

        return completedTask == completion.Task
            ? await completion.Task
            : null;
    }
}
