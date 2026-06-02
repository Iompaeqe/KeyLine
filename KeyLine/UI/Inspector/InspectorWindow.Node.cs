using System.Windows;

namespace KeyLine.UI.Inspector;

public partial class InspectorWindow
{
    public void SetNodeContent(UIElement? content, bool hasContent, bool isCollapsed)
    {
        NodeSectionHost.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
        NodeHeader.ArrowText = isCollapsed ? "▶" : "▼";
        NodeContentPanel.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        NodeHeader.Margin = new Thickness(0, 0, 0, isCollapsed ? 0 : 4);

        NodeContentPanel.Children.Clear();

        if (content != null)
            NodeContentPanel.Children.Add(content);

        RequestScrollVisibilityUpdate();
    }
}