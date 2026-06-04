using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using KeyLine.Interop;

namespace KeyLine;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const int GlobalToolTipInitialShowDelayMilliseconds = 350;

    static App()
    {
        ToolTipService.InitialShowDelayProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(GlobalToolTipInitialShowDelayMilliseconds));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        base.OnStartup(e);
    }
}
