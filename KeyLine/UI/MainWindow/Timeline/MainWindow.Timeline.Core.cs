using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyLine.Domain;
using KeyLine.Services.Macro;
using KeyLine.Services.Playback;
using KeyLine.Services.Timeline;
using KeyLine.State;
using KeyLine.UI.Config;
using KeyLine.UI.Nodes;
using KeyLine.UI.Timeline;

namespace KeyLine;

public partial class MainWindow
{
    private static TimelineUiConfig TimelineUi => GeneratedUiConfig.Timeline;

    private static double TimelineRowHeight => TimelineUi.RowHeight;
    private static double TimelineRowGap => TimelineUi.RowGap;
    private static double TimelineHeaderWidth => TimelineUi.HeaderWidth;

    private static double TimelineFirstItemLeft => TimelineUi.FirstItemLeft;
    private static double TimelineItemGap => TimelineUi.ItemGap;
    private static double TimelineRightPadding => TimelineUi.RightPadding;

    private static double TimelineConnectorY => TimelineUi.ConnectorY;
    private static double TimelineConnectorThickness => TimelineUi.ConnectorThickness;

    private static double TimelineHeaderTopExtra => TimelineUi.HeaderTopExtra;
    private static double TimelineHeaderBottomExtra => TimelineUi.HeaderBottomExtra;

    private readonly Dictionary<MacroTimeline, TimelineRowRenderState> _timelineRowRenderStates = new();
    private readonly Dictionary<MacroTimeline, TextBlock> _timelineHeaderStatusTextBlocks = new();

    private sealed class TimelineRowVisualModel
    {
        public required MacroTimeline Timeline { get; init; }
        public required IReadOnlyList<TimelineVisualItem> VisualItems { get; init; }
        public required bool IsFirstRow { get; init; }
        public required bool IsLastRow { get; init; }

        public double RowWidth
        {
            get
            {
                if (VisualItems.Count == 0)
                    return TimelineFirstItemLeft + TimelineRightPadding;

                var lastItem = VisualItems[^1];
                return lastItem.Left + lastItem.Width + TimelineRightPadding;
            }
        }
    }

    private void RefreshTimeline(object? sender = null, RoutedEventArgs? e = null)
    {
        RefreshTimelineCore(refreshInspector: true);
    }

    private void RefreshTimelineWithoutInspector()
    {
        RefreshTimelineCore(refreshInspector: false);
    }

    private void RefreshTimelineCore(bool refreshInspector)
    {
        if (TimelineRowsPanel == null)
            return;

        TimelineRowsPanel.Children.Clear();
        _timelineRowRenderStates.Clear();
        BuildTimelineHeaderGridRows();
        UpdateSingleTimelineMetadataText();

        if (_document.Timelines.Count == 0)
        {
            ShowEmptyTimelineState();
            UpdateTimelineOptionsPagerVisibility();
            UpdateWindowHeightForTimelineCount();
            Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
            return;
        }

        HideEmptyTimelineState();
        if (refreshInspector)
            SyncOptionsFromActiveTimeline();

        TimelineRowsPanel.Margin = TimelineLayoutCalculator.GetRowsPanelMargin(TimelineHeaderTopExtra);

        var rowModels = BuildTimelineRowVisualModels();

        var canvasWidth = GetTimelineCanvasWidth(rowModels);
        SetTimelineCanvasWidthForAllRows(canvasWidth);

        for (var i = 0; i < rowModels.Count; i++)
        {
            var rowModel = rowModels[i];
            var timeline = rowModel.Timeline;

            if (_document.Timelines.Count > 1)
            {
                var isActive = ReferenceEquals(timeline, _document.ActiveTimeline);
                var isSelected = _selection.IsTimelineSelected(timeline);

                AddTimelineHeaderToGrid(
                    timeline,
                    i,
                    isActive,
                    isSelected);
            }

            TimelineRowsPanel.Children.Add(CreateTimelineRow(
                timeline,
                rowModel.VisualItems,
                canvasWidth,
                rowModel.IsFirstRow,
                rowModel.IsLastRow));
        }

        UpdateTimelineOptionsPagerVisibility();
        UpdateWindowHeightForTimelineCount();

        Dispatcher.BeginInvoke(new Action(UpdateTimelineScrollIndicator));
    }

    private void BuildTimelineHeaderGridRows()
    {
        TimelineHeaderGrid.Children.Clear();
        TimelineHeaderGrid.RowDefinitions.Clear();
        _timelineHeaderStatusTextBlocks.Clear();

        var showHeaderColumn = _document.Timelines.Count > 1;

        TimelineHeaderColumn.Width = showHeaderColumn
            ? new GridLength(TimelineHeaderWidth)
            : new GridLength(0);

        if (!showHeaderColumn)
        {
            TimelineHeaderGrid.Visibility = Visibility.Collapsed;
            return;
        }

        TimelineHeaderGrid.Visibility = Visibility.Visible;

        TimelineHeaderGrid.RowDefinitions.Add(
            TimelineLayoutCalculator.CreateHeaderTopExtraRow(TimelineHeaderTopExtra));

        for (var i = 0; i < _document.Timelines.Count; i++)
        {
            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderContentRow(TimelineRowHeight));

            TimelineHeaderGrid.RowDefinitions.Add(
                TimelineLayoutCalculator.CreateTimelineHeaderGapRow(
                    i,
                    _document.Timelines.Count,
                    TimelineRowGap,
                    TimelineHeaderBottomExtra));
        }

        var headerColumnBackplate = new Border
        {
            CornerRadius = new CornerRadius(8, 0, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(8, 17, 31)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 64, 100)),
            BorderThickness = new Thickness(1, 1, 1, 1),
            IsHitTestVisible = false
        };

        Grid.SetRow(headerColumnBackplate, 0);
        Grid.SetRowSpan(headerColumnBackplate, TimelineHeaderGrid.RowDefinitions.Count);
        TimelineHeaderGrid.Children.Add(headerColumnBackplate);
    }

    private void AddTimelineHeaderToGrid(MacroTimeline timeline, int timelineIndex, bool isActive, bool isSelected)
    {
        var isFirst = timelineIndex == 0;
        var isLast = timelineIndex == _document.Timelines.Count - 1;
        var header = CreateTimelineHeader(timeline, isActive, isSelected, isFirst, isLast);

        var row = TimelineLayoutCalculator.GetHeaderGridRow(timelineIndex);
        var rowSpan = TimelineLayoutCalculator.GetHeaderGridRowSpan(timelineIndex);

        Grid.SetRow(header, row);
        Grid.SetRowSpan(header, rowSpan);

        TimelineHeaderGrid.Children.Add(header);
    }

    private void ShowEmptyTimelineState()
    {
        TimelineRowsPanel.Margin = new Thickness(0);
        TimelineHeaderGrid.Visibility = Visibility.Collapsed;
        TimelineHeaderColumn.Width = new GridLength(0);
        SingleTimelineMetadataText.Visibility = Visibility.Collapsed;

        EmptyTimelinePanel.Visibility = Visibility.Visible;
        TimelineScrollViewer.Visibility = Visibility.Hidden;
        TimelineDragOverlayCanvas.Visibility = Visibility.Collapsed;
        TimelineScrollIndicator.Visibility = Visibility.Collapsed;
    }

    private void HideEmptyTimelineState()
    {
        EmptyTimelinePanel.Visibility = Visibility.Collapsed;
        TimelineScrollViewer.Visibility = Visibility.Visible;
        TimelineDragOverlayCanvas.Visibility = Visibility.Visible;
        TimelineScrollIndicator.Visibility = Visibility.Visible;
        UpdateSingleTimelineMetadataText();
    }

}
