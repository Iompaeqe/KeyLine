using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KeySpammer;

public partial class MainWindow
{
    private const double MacroTabsDragThreshold = 4;
    private const double MacroTabsWheelScrollAmount = 48;

    private void MacroTabsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var direction = e.Delta > 0 ? -1 : 1;
        MacroTabsScrollViewer.ScrollToHorizontalOffset(
            MacroTabsScrollViewer.HorizontalOffset + (direction * MacroTabsWheelScrollAmount));

        e.Handled = true;
    }

    private void MacroTabsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateMacroTabEdgeIndicators();
    }

    private void MacroTabsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMacroTabEdgeIndicators();
    }

    private void MacroTabsScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsSourceInsideTextBox(e.OriginalSource as DependencyObject))
            return;

        _isDraggingMacroTabs = true;
        _didDragMacroTabs = false;
        _macroTabsDragStartPoint = e.GetPosition(MacroTabsScrollViewer);
        _macroTabsDragStartOffset = MacroTabsScrollViewer.HorizontalOffset;
    }

    private void MacroTabsScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingMacroTabs || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(MacroTabsScrollViewer);
        var deltaX = currentPoint.X - _macroTabsDragStartPoint.X;

        if (!_didDragMacroTabs && Math.Abs(deltaX) < MacroTabsDragThreshold)
            return;

        _didDragMacroTabs = true;
        MacroTabsScrollViewer.ScrollToHorizontalOffset(_macroTabsDragStartOffset - deltaX);

        if (!MacroTabsScrollViewer.IsMouseCaptured)
            MacroTabsScrollViewer.CaptureMouse();

        e.Handled = true;
    }

    private void MacroTabsScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndMacroTabsDrag();
    }

    private void MacroTabsScrollViewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            EndMacroTabsDrag();
    }

    private void EndMacroTabsDrag()
    {
        _isDraggingMacroTabs = false;

        if (MacroTabsScrollViewer?.IsMouseCaptured == true)
            MacroTabsScrollViewer.ReleaseMouseCapture();
    }

    private void UpdateMacroTabEdgeIndicators()
    {
        if (MacroTabsScrollViewer == null ||
            MacroTabsLeftEdgeFade == null ||
            MacroTabsRightEdgeFade == null ||
            MacroTabsLeftEdgeLine == null ||
            MacroTabsRightEdgeLine == null)
        {
            return;
        }

        var hasOverflow = MacroTabsScrollViewer.ScrollableWidth > 0.5;
        var canScrollLeft = hasOverflow && MacroTabsScrollViewer.HorizontalOffset > 0.5;
        var canScrollRight = hasOverflow &&
                             MacroTabsScrollViewer.HorizontalOffset < MacroTabsScrollViewer.ScrollableWidth - 0.5;

        MacroTabsLeftEdgeFade.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsLeftEdgeLine.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsRightEdgeFade.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        MacroTabsRightEdgeLine.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsSourceInsideTextBox(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is TextBox)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
