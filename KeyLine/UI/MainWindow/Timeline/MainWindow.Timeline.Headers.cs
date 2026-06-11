using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine;

public partial class MainWindow
{
    // Tag carried by the collapse chevron in TimelineHeader.xaml so the header's mouse handler can
    // tell a chevron click apart from a card click/drag.
    private const string CollapseToggleTag = "collapse-toggle";

    private static bool IsCollapseToggleSource(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is FrameworkElement element && (element.Tag as string) == CollapseToggleTag)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private ContextMenu CreateTimelineHeaderContextMenu(MacroTimeline timeline)
    {
        var contextMenu = new ContextMenu();
        contextMenu.SetResourceReference(FrameworkElement.StyleProperty, "KeyLineContextMenu");

        // Hook timelines (Start/End) are pinned and fixed: no duplicate/rename/delete.
        var isHook = IsHookTimeline(timeline);

        var duplicateItem = new MenuItem
        {
            Header = "Duplicate",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        duplicateItem.Click += (_, _) =>
        {
            ResetTimelineDeleteConfirmation();
            DuplicateTimeline(timeline);
        };

        var renameItem = new MenuItem
        {
            Header = "Rename",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        renameItem.Click += (_, _) => BeginTimelineHeaderRename(timeline);

        // Disable/Enable lets a timeline stay authored but be skipped by every loop mode at run time.
        var disableItem = new MenuItem
        {
            Header = timeline.IsDisabled ? "Enable" : "Disable",
            IsEnabled = _isTimelineEditingEnabled && !isHook
        };
        disableItem.Click += (_, _) => ToggleTimelineDisabled(timeline);

        var deleteItem = new MenuItem
        {
            Header = "Delete",
            IsEnabled = _isTimelineEditingEnabled && !isHook && _document.Timelines.Count > 1
        };
        deleteItem.Click += (_, _) => BeginTimelineDeleteConfirmation(timeline);

        contextMenu.Items.Add(duplicateItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(renameItem);
        contextMenu.Items.Add(disableItem);
        contextMenu.Items.Add(deleteItem);

        return contextMenu;
    }

    private void UpdateWindowHeightForTimelineCount()
    {
        var displayTimelines = GetDisplayTimelines();

        // Sum per-row heights and gaps so collapsed rows shrink the window accordingly.
        var timelineAreaHeight = 28.0;
        for (var i = 0; i < displayTimelines.Count; i++)
        {
            timelineAreaHeight += GetDisplayRowHeight(displayTimelines[i]);
            if (i < displayTimelines.Count - 1)
                timelineAreaHeight += GetDisplayRowGap(displayTimelines[i]);
        }

        if (displayTimelines.Count == 0)
            timelineAreaHeight += TimelineRowHeight;

        var wantedHeight = 252 + timelineAreaHeight;

        LockWindowHeight(Math.Max(420, wantedHeight));
    }

    private void LockWindowHeight(double height)
    {
        height = Math.Ceiling(height);

        // The window is intentionally horizontally resizable only.
        // MinHeight == MaxHeight blocks manual vertical resizing, while this
        // method still lets the app grow/shrink vertically when timeline count changes.
        MinHeight = height;
        MaxHeight = height;
        Height = height;
    }
}
