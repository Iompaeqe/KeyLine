using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyLine.Interop;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        HideFromAltTab();
    }

    private void RequestScrollVisibilityUpdate()
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(UpdateScrollBarVisibility));
    }

    private void UpdateScrollBarVisibility()
    {
        InspectorScrollViewer.UpdateLayout();
        InspectorScrollViewer.VerticalScrollBarVisibility =
            InspectorScrollViewer.ExtentHeight > InspectorScrollViewer.ViewportHeight + 1
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Hidden;
    }

    private void HideFromAltTab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        var exStyle = NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(handle, NativeMethods.GWL_EXSTYLE, exStyle);
    }
}