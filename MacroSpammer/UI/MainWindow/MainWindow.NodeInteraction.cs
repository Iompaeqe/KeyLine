using System.Windows;
using System.Windows.Input;
using MacroSpammer.Domain;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Nodes;
using MacroSpammer.UI.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using MacroSpammer.Services.Macro;
using MacroSpammer.Services.Timeline;
using System.Windows.Documents;
using System.Windows.Media;

namespace MacroSpammer;

public partial class MainWindow
{
    // From MainWindow.TimelineDragging.cs
        private readonly NodeDragPreviewModel _nodeDragPreview = new();
        private NodeDragGhostController? _nodeDragGhost;

        private NodeDragGhostController NodeDragGhost => _nodeDragGhost ??= new NodeDragGhostController(TimelineDragOverlayCanvas);
        private bool _timelineDragGlobalHandlersAttached;
        private bool _isDelayValueMouseEditPending;
        private bool _isMouseNodeEditPending;
        private MacroTimeline? _pendingClickSelectionTimeline;
        private MacroNode? _pendingClickSelectionStep;
        private ModifierKeys _pendingClickSelectionModifiers;
        private bool _pendingClickWasSelected;

        private static DragUiConfig DragUi => GeneratedUiConfig.Drag;

        private static double NodeDragThreshold => DragUi.NodeDragThreshold;
        private static double TimelineHeaderDragThreshold => DragUi.TimelineHeaderDragThreshold;

        private void AttachNodeMouseHandlers(NodeBase nodeControl, MacroTimeline timeline, MacroNode node)
        {
            nodeControl.PreviewMouseLeftButtonDown += (_, e) =>
            {
                EnsureTimelineDragGlobalHandlers();
                var inlineEditorActivation = nodeControl.GetInlineEditorActivationMode(e.OriginalSource as DependencyObject);

                if (inlineEditorActivation != InlineEditorActivationMode.None)
                {
                    SelectStepFromPointer(timeline, node);

                    if (!_isTimelineEditingEnabled)
                    {
                        e.Handled = true;
                        return;
                    }

                    CancelTimelineDragState();
                    SaveUndoSnapshot();
                    _isDelayValueMouseEditPending = inlineEditorActivation == InlineEditorActivationMode.SuppressMouseUp;
                    _isMouseNodeEditPending = inlineEditorActivation == InlineEditorActivationMode.AllowMouseUp;
                    nodeControl.FocusInlineEditor(e.OriginalSource as DependencyObject);
                    e.Handled = true;
                    return;
                }

                if (e.ClickCount >= 2)
                {
                    SelectStepFromPointer(timeline, node);
                    OpenInspectorFromSelection();
                    e.Handled = true;
                    return;
                }

                var modifiers = Keyboard.Modifiers;
                var wasSelected = _selection.IsNodeSelected(timeline, node);
                var shouldDeferSelectionCollapse =
                    wasSelected &&
                    modifiers == ModifierKeys.None &&
                    _selection.HasMultipleNodeSelection;

                if (shouldDeferSelectionCollapse)
                {
                    SetPendingClickSelection(timeline, node, modifiers, wasSelected);
                }
                else
                {
                    ClearPendingClickSelection();
                    SelectStepFromPointer(timeline, node);
                }

                if (!_isTimelineEditingEnabled)
                {
                    e.Handled = true;
                    return;
                }

                _drag.BeginStepDrag(timeline, node, e.GetPosition(TimelineRowsPanel));

                nodeControl.CaptureMouse();

                e.Handled = true;
            };

            nodeControl.PreviewMouseMove += (_, e) =>
            {
                if (!_isTimelineEditingEnabled)
                    return;

                if (_drag.DraggedNode == null ||
                    _drag.DraggedNodeTimeline == null ||
                    e.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                var currentPoint = e.GetPosition(TimelineRowsPanel);

                if (!_drag.IsDraggingNode)
                {
                    if (!_drag.ShouldStartStepDrag(currentPoint, NodeDragThreshold))
                        return;

                    _drag.MarkStepDragging();

                    ApplyPendingSelectionForDrag();
                    BeginStepDragPreviewModel(_drag.DraggedNodeTimeline, _drag.DraggedNode);
                    UpdateStepDragPreviewFromMouse(currentPoint);

                    BeginWindowLevelStepDragCapture(nodeControl);
                    BeginDraggedStepGhost();

                    RefreshTimelineDragPreview();
                }

                var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

                if (previewChanged)
                    RefreshTimelineDragPreview();

                UpdateDraggedStepGhostTargetPosition();

                e.Handled = true;
            };

            nodeControl.PreviewMouseLeftButtonUp += (_, e) =>
            {
                if (_isDelayValueMouseEditPending)
                {
                    _isDelayValueMouseEditPending = false;
                    e.Handled = true;
                    return;
                }

                if (_isMouseNodeEditPending)
                {
                    _isMouseNodeEditPending = false;
                    return;
                }

                if (!_drag.IsDraggingNode)
                    ApplyPendingClickSelection();

                CompleteStepDrop();
                e.Handled = true;
            };

            nodeControl.LostMouseCapture += (_, _) =>
            {
                if (Mouse.LeftButton != MouseButtonState.Pressed && _drag.DraggedNode != null)
                    CancelTimelineDragState();
            };

            nodeControl.PreviewMouseRightButtonDown += (_, e) =>
            {
                CancelTimelineDragState();
                SelectStepFromPointer(timeline, node);

                if (!_isTimelineEditingEnabled)
                {
                    e.Handled = true;
                    return;
                }

                DeleteStep(timeline, node);

                e.Handled = true;
            };
        }

        private void SetPendingClickSelection(MacroTimeline timeline, MacroNode node, ModifierKeys modifiers, bool wasSelected)
        {
            _pendingClickSelectionTimeline = timeline;
            _pendingClickSelectionStep = node;
            _pendingClickSelectionModifiers = modifiers;
            _pendingClickWasSelected = wasSelected;
        }

        private void ApplyPendingClickSelection()
        {
            if (_pendingClickSelectionTimeline != null && _pendingClickSelectionStep != null)
                SelectStepFromStoredClick(
                    _pendingClickSelectionTimeline,
                    _pendingClickSelectionStep,
                    _pendingClickSelectionModifiers);

            ClearPendingClickSelection();
        }

        private void ApplyPendingSelectionForDrag()
        {
            if (_pendingClickSelectionTimeline == null || _pendingClickSelectionStep == null)
                return;

            if (!_pendingClickWasSelected && _pendingClickSelectionModifiers == ModifierKeys.None)
            {
                var previousTimeline = _selection.SelectedTimeline;
                SelectTimeline(_pendingClickSelectionTimeline, refreshInspector: false);
                _selection.SelectNode(_pendingClickSelectionTimeline, _pendingClickSelectionStep);
                UpdateSelectionVisuals(previousTimeline, _selection.SelectedTimeline);
                RefreshInspectorDeferred();
            }

            ClearPendingClickSelection();
        }

        private void SelectStepFromStoredClick(MacroTimeline timeline, MacroNode node, ModifierKeys modifiers)
        {
            var previousTimeline = _selection.SelectedTimeline;

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                SelectTimeline(timeline, refreshInspector: false);
                ToggleStepSelection(timeline, node);
                UpdateSelectionVisuals(previousTimeline, _selection.SelectedTimeline);
                RefreshInspectorDeferred();
                return;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift) && _selection.AnchorNode != null &&
                ReferenceEquals(_selection.SelectedTimeline, timeline))
            {
                SelectTimeline(timeline, refreshInspector: false);
                SelectStepRange(timeline, node);
                UpdateSelectionVisuals(previousTimeline, _selection.SelectedTimeline);
                RefreshInspectorDeferred();
                return;
            }

            SelectTimeline(timeline, refreshInspector: false);
            _selection.SelectNode(timeline, node);
            UpdateSelectionVisuals(previousTimeline, _selection.SelectedTimeline);
            RefreshInspectorDeferred();
        }

        private void ClearPendingClickSelection()
        {
            _pendingClickSelectionTimeline = null;
            _pendingClickSelectionStep = null;
            _pendingClickSelectionModifiers = ModifierKeys.None;
            _pendingClickWasSelected = false;
        }

    // From MainWindow.TimelineDragCapture.cs
        private void EnsureTimelineDragGlobalHandlers()
        {
            if (_timelineDragGlobalHandlersAttached)
                return;

            _timelineDragGlobalHandlersAttached = true;

            AddHandler(
                Mouse.PreviewMouseUpEvent,
                new MouseButtonEventHandler(Window_PreviewMouseUpForTimelineDrag),
                true);

            AddHandler(
                Mouse.PreviewMouseMoveEvent,
                new MouseEventHandler(Window_PreviewMouseMoveForTimelineDrag),
                true);
        }

        private void BeginWindowLevelStepDragCapture(FrameworkElement originalElement)
        {
            if (originalElement.IsMouseCaptured)
                originalElement.ReleaseMouseCapture();

            Mouse.Capture(this, CaptureMode.SubTree);
        }

        private void Window_PreviewMouseMoveForTimelineDrag(object sender, MouseEventArgs e)
        {
            if (!_drag.IsDraggingNode ||
                _drag.DraggedNodeTimeline == null ||
                _drag.DraggedNode == null ||
                e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(TimelineRowsPanel);
            var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

            if (previewChanged)
                RefreshTimelineDragPreview();

            UpdateDraggedStepGhostTargetPosition();

            e.Handled = true;
        }

        private void Window_PreviewMouseUpForTimelineDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (!_drag.IsDraggingNode && !_drag.IsDraggingTimelineHeader)
                return;

            CompleteStepDrop();

            e.Handled = true;
        }

    // From MainWindow.TimelineDragGhost.cs
        private void BeginDraggedStepGhost()
        {
            if (_drag.DraggedNodeTimeline == null || _drag.DraggedNode == null)
                return;

            EndDraggedStepGhost();

            var ghost = CreateDraggedStepGhostElement(_drag.DraggedNodeTimeline, _drag.DraggedNode);
            if (ghost is not FrameworkElement ghostElement)
                return;

            var size = MainWindow.MeasureTimelineItem(ghostElement);
            NodeDragGhost.Begin(
                ghostElement,
                size,
                MainWindow.DragUi.GhostOpacity,
                MainWindow.DragUi.GhostScale,
                GetDraggedStepGhostTargetPosition(size));

            StartDragGhostAnimation();
        }

        private UIElement CreateDraggedStepGhostElement(MacroTimeline timeline, MacroNode draggedNode)
        {
            var ghostSteps = GetDraggedDisplaySteps(timeline, draggedNode);
            if (ghostSteps.Count <= 1)
                return CreateNode(timeline, draggedNode);

            var canvas = new Canvas
            {
                Height = TimelineRowHeight,
                IsHitTestVisible = false
            };

            var currentLeft = 0.0;
            var maxHeight = 0.0;
            foreach (var step in ghostSteps)
            {
                var block = CreateNode(timeline, step);
                if (block is not FrameworkElement element)
                    continue;

                element.IsHitTestVisible = false;
                var size = MainWindow.MeasureTimelineItem(element);
                Canvas.SetLeft(element, currentLeft);
                Canvas.SetTop(element, TimelineLayoutCalculator.GetItemTop(TimelineConnectorY, size.Height));
                canvas.Children.Add(element);

                currentLeft += size.Width + TimelineItemGap;
                maxHeight = Math.Max(maxHeight, size.Height);
            }

            canvas.Width = Math.Max(1, currentLeft - TimelineItemGap);
            canvas.Height = Math.Max(1, maxHeight);
            return canvas;
        }

        private List<MacroNode> GetDraggedDisplaySteps(MacroTimeline timeline, MacroNode draggedNode)
        {
            if (!_selection.HasMultipleNodeSelection || !_selection.IsNodeSelected(timeline, draggedNode))
                return new List<MacroNode> { draggedNode };

            var visibleSteps = MacroTimelineBuilder.BuildVisibleSteps(
                timeline.Nodes.ToList(),
                timeline.UseStandardDelay,
                timeline.ShowKeyUpDown);

            return visibleSteps
                .Where(step => !step.IsSyntheticDisplayNode
                    ? _selection.IsNodeSelected(timeline, step)
                    : _selection.SelectedNodes.Any(selectedStep => IsSameSelectedStep(step, selectedStep)))
                .ToList();
        }

        private void UpdateDraggedStepGhostTargetPosition(bool snap = false)
        {
            if (!NodeDragGhost.HasGhost)
                return;

            if (!_drag.IsDraggingNode || _drag.DraggedNodeTimeline == null)
                return;

            NodeDragGhost.UpdateTarget(
                GetDraggedStepGhostTargetPosition(new Size(NodeDragGhost.Width, NodeDragGhost.Height)),
                snap);
        }

        private Point GetDraggedStepGhostTargetPosition(Size ghostSize)
        {
            if (_drag.DraggedNodeTimeline == null)
                return default;

            var mouse = Mouse.GetPosition(TimelineDragOverlayCanvas);
            var rowTopInRowsPanel = GetTimelineRowTopY(_drag.DraggedNodeTimeline);

            var targetX = mouse.X - (ghostSize.Width / 2.0) + MainWindow.DragUi.GhostCursorOffsetX;
            var targetY = rowTopInRowsPanel + MainWindow.TimelineConnectorY - (ghostSize.Height / 2.0) + MainWindow.DragUi.GhostCursorOffsetY;

            return new Point(targetX, targetY);
        }

        private void StartDragGhostAnimation()
        {
            NodeDragGhost.StartAnimation(DragGhost_Rendering);
        }

        private void StopDragGhostAnimation()
        {
            NodeDragGhost.StopAnimation(DragGhost_Rendering);
        }

        private void DragGhost_Rendering(object? sender, EventArgs e)
        {
            if (!NodeDragGhost.HasGhost)
                return;

            if (!_drag.IsDraggingNode)
                return;

            if (ApplyStepDragAutoScroll())
            {
                var currentPoint = Mouse.GetPosition(TimelineRowsPanel);
                var previewChanged = UpdateStepDragPreviewFromMouse(currentPoint);

                if (previewChanged)
                    RefreshTimelineDragPreview();
            }

            UpdateDraggedStepGhostTargetPosition();
            NodeDragGhost.MoveTowardTarget(MainWindow.DragUi.GhostFollowStrength);
        }

        private bool ApplyStepDragAutoScroll()
        {
            if (TimelineScrollViewer == null || TimelineRowsPanel == null)
                return false;

            if (!HasTimelineOverflow(TimelineScrollViewer.ExtentWidth, TimelineScrollViewer.ViewportWidth))
                return false;

            var mouse = Mouse.GetPosition(TimelineScrollViewer);
            var viewportWidth = TimelineScrollViewer.ViewportWidth > 0
                ? TimelineScrollViewer.ViewportWidth
                : TimelineScrollViewer.ActualWidth;

            if (viewportWidth <= 0)
                return false;

            var edgeSize = Math.Min(MainWindow.DragUi.AutoScrollEdgeSize, viewportWidth / 2.0);
            if (edgeSize <= 0)
                return false;

            var scrollStep = 0.0;

            if (mouse.X < edgeSize)
            {
                var strength = (edgeSize - mouse.X) / edgeSize;
                scrollStep = -MainWindow.DragUi.AutoScrollMaxStep * Math.Clamp(strength, 0, 1);
            }
            else if (mouse.X > viewportWidth - edgeSize)
            {
                var strength = (mouse.X - (viewportWidth - edgeSize)) / edgeSize;
                scrollStep = MainWindow.DragUi.AutoScrollMaxStep * Math.Clamp(strength, 0, 1);
            }

            if (Math.Abs(scrollStep) < 0.1)
                return false;

            var previousOffset = TimelineScrollViewer.HorizontalOffset;
            var maxOffset = Math.Max(0, TimelineScrollViewer.ExtentWidth - TimelineScrollViewer.ViewportWidth);
            var targetOffset = Math.Clamp(previousOffset + scrollStep, 0, maxOffset);

            if (Math.Abs(targetOffset - previousOffset) < 0.1)
                return false;

            TimelineScrollViewer.ScrollToHorizontalOffset(targetOffset);
            UpdateTimelineScrollIndicator();
            return true;
        }

        private void EndDraggedStepGhost()
        {
            StopDragGhostAnimation();
            NodeDragGhost.End();
        }

    // From MainWindow.NodeDragLifecycle.cs
        private void CompleteStepDrop()
        {
            MacroTimeline? dropTimeline = null;
            var changed = false;

            if (_drag.IsDraggingNode && _drag.DraggedNodeTimeline != null && _drag.DraggedNode != null)
            {
                SaveUndoSnapshot();
                CaptureDroppedGhostPositionForAnimation();

                dropTimeline = _drag.DraggedNodeTimeline;
                changed = MoveStepBeforeRawAnchor(
                    dropTimeline,
                    _drag.DraggedNode,
                    _drag.NodeDropRawInsertAnchor);

                if (changed)
                {
                    SeedDraggedNodeAnimationFromGhost();
                    MergeAdjacentDelayNodesIfEnabled(dropTimeline);
                }
            }

            CancelTimelineDragState();

            if (dropTimeline != null)
                RefreshTimelineRow(dropTimeline);

            if (changed)
                ScheduleSaveState();

            RefreshInspector();

            NodeDragGhost.ClearDropPosition();
        }

        private void CancelTimelineDragState()
        {
            EndDraggedStepGhost();

            _nodeDragPreview.Clear();
            ClearPendingClickSelection();

            if (_drag.DraggedNodeTimeline != null)
                RemoveDropPlaceholderAnimationKeys(_drag.DraggedNodeTimeline);

            _drag.EndStepDrag();
            _drag.EndTimelineHeaderDrag();
            _drag.EndTimelinePan();

            if (Mouse.Captured != null)
                Mouse.Capture(null);
        }

    // From MainWindow.NodeDragPreview.cs
        private void BeginStepDragPreviewModel(MacroTimeline timeline, MacroNode draggedNode)
        {
            _nodeDragPreview.Clear();
            RemoveDropPlaceholderAnimationKeys(timeline);

            var draggedItems = GetRawStepsForDrag(timeline, draggedNode);
            if (draggedItems.Count == 0)
                return;

            _nodeDragPreview.Begin(
                timeline,
                timeline.Nodes,
                draggedItems,
                _drag.DraggedNode,
                measuredNode => GetCachedNodePreviewWidth(timeline, measuredNode),
                TimelineFirstItemLeft,
                TimelineItemGap);

            _drag.UpdateStepDragPreview(_drag.NodeDragCurrentPoint, GetStepDragPreviewRawInsertAnchor());
        }

        private bool UpdateStepDragPreviewFromMouse(Point currentPoint)
        {
            var orderChanged = _nodeDragPreview.UpdateFromMouse(
                currentPoint,
                DragUi.PreviewMouseMoveEpsilon,
                TimelineFirstItemLeft,
                TimelineItemGap);

            var rawInsertAnchor = GetStepDragPreviewRawInsertAnchor();
            var anchorChanged = _drag.UpdateStepDragPreview(currentPoint, rawInsertAnchor);

            return orderChanged || anchorChanged;
        }

        private MacroNode? GetStepDragPreviewRawInsertAnchor()
        {
            return _nodeDragPreview.GetRawInsertAnchor();
        }

        private IReadOnlyList<MacroNode> GetTimelineRenderRawSteps(MacroTimeline timeline)
        {
            if (_drag.IsDraggingNode &&
                ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
                _nodeDragPreview.HasPreviewRawSteps)
            {
                return _nodeDragPreview.PreviewRawSteps;
            }

            return timeline.Nodes;
        }

        private IReadOnlyList<NodePreviewSlot> GetTimelineRenderPreviewSlots(MacroTimeline timeline)
        {
            if (_drag.IsDraggingNode &&
                ReferenceEquals(_drag.DraggedNodeTimeline, timeline) &&
                _nodeDragPreview.HasSlots)
            {
                return _nodeDragPreview.Slots;
            }

            return Array.Empty<NodePreviewSlot>();
        }

        private double GetTimelineRowTopY(MacroTimeline timeline)
        {
            var index = _document.Timelines.IndexOf(timeline);
            if (index < 0)
                return 0;

            return index * (TimelineRowHeight + TimelineRowGap);
        }

    // From MainWindow.NodeDropAnimation.cs
        private void CaptureDroppedGhostPositionForAnimation()
        {
            if (_drag.DraggedNode == null || _drag.DraggedNodeTimeline == null)
            {
                NodeDragGhost.ClearDropPosition();
                return;
            }

            NodeDragGhost.CaptureDropPosition(
                TimelineRowsPanel,
                TimelineLayoutCalculator.GetItemTop(MainWindow.TimelineConnectorY, NodeDragGhost.Height));
        }

        private void SeedDraggedNodeAnimationFromGhost()
        {
            if (NodeDragGhost.LastDroppedRowsPanelPosition == null ||
                _drag.DraggedNode == null ||
                _drag.DraggedNodeTimeline == null)
            {
                return;
            }

            var animationKey = GetTimelineAnimationKey(_drag.DraggedNodeTimeline, _drag.DraggedNode);
            _timelineVisualPositions[animationKey] = NodeDragGhost.LastDroppedRowsPanelPosition.Value;
        }

    // From MainWindow.NodeReordering.cs
        private bool MoveStepBeforeRawAnchor(
            MacroTimeline timeline,
            MacroNode draggedNode,
            MacroNode? rawInsertAnchor)
        {
            var draggedItems = GetRawStepsForDrag(timeline, draggedNode);
            if (draggedItems.Count == 0)
                return false;

            if (_selection.HasMultipleNodeSelection &&
                _selection.IsNodeSelected(timeline, draggedNode) &&
                TryApplyStepDragPreviewOrder(timeline, draggedNode))
            {
                return true;
            }

            var changed = TimelineNodeMutationService.MoveRawStepsBeforeAnchor(
                timeline,
                draggedItems,
                rawInsertAnchor);

            if (!changed)
                return false;

            if (_selection.IsNodeSelected(timeline, draggedNode))
            {
                var updatedTimelineNodeSet = timeline.Nodes.ToHashSet();
                _selection.SelectNodes(
                    timeline,
                    _selection.SelectedNodes.Where(step => updatedTimelineNodeSet.Contains(step)).ToList(),
                    _selection.AnchorNode);
            }
            else
            {
                _selection.SelectNode(timeline, draggedNode);
            }

            return true;
        }

        private bool TryApplyStepDragPreviewOrder(MacroTimeline timeline, MacroNode draggedNode)
        {
            if (_nodeDragPreview.PreviewRawSteps.Count != timeline.Nodes.Count)
            {
                return false;
            }

            var timelineNodeSet = timeline.Nodes.ToHashSet();
            if (_nodeDragPreview.PreviewRawSteps.Any(step => !timelineNodeSet.Contains(step)))
                return false;

            if (_nodeDragPreview.PreviewRawSteps.SequenceEqual(timeline.Nodes))
                return false;

            timeline.Nodes.Clear();
            foreach (var step in _nodeDragPreview.PreviewRawSteps)
                timeline.Nodes.Add(step);

            var updatedTimelineNodeSet = timeline.Nodes.ToHashSet();
            _selection.SelectNodes(
                timeline,
                _selection.SelectedNodes.Where(step => updatedTimelineNodeSet.Contains(step)).ToList(),
                _selection.AnchorNode);
            if (!_selection.HasNodeSelection)
                _selection.SelectNode(timeline, draggedNode);

            return true;
        }

        private List<MacroNode> GetRawStepsForDrag(MacroTimeline timeline, MacroNode draggedNode)
        {
            return _selection.IsNodeSelected(timeline, draggedNode)
                ? GetSelectedRawSteps(timeline)
                : TimelineNodeMutationService.GetRawStepsForDisplayStep(timeline, draggedNode);
        }

    // From MainWindow.NodeEditing.cs
        private void DeleteSelectedItem()
        {
            if (AnyPlaybackRunning())
                return;

            ResetClearConfirmation();
            ResetTimelineDeleteConfirmation();

            if (_selection.HasNodeSelection)
            {
                DeleteSelectedStep();
                return;
            }

            if (_selection.HasTimelineSelection && _selection.SelectedTimeline != null)
                DeleteSelectedTimeline(_selection.SelectedTimeline);
        }

        private void DeleteSelectedTimeline(MacroTimeline timeline)
        {
            ResetTimelineDeleteConfirmation();

            SaveUndoSnapshot();
            if (TryGetTimelineRunner(timeline, out var runner))
                runner.Stop();

            RemoveTimelineRunner(timeline);

            _document.RemoveTimeline(timeline);
            _selection.Clear();

            if (_document.Timelines.Count > 0)
                SelectTimeline(_document.ActiveTimeline);

            RefreshTimeline();
            ScheduleSaveState();
        }

        private void DeleteSelectedStep()
        {
            var timeline = _selection.SelectedTimeline;

            if (timeline == null || _selection.SelectedNode == null)
                return;

            if (!_selection.HasMultipleNodeSelection)
            {
                DeleteStep(timeline, _selection.SelectedNode);
                return;
            }

            DeleteSteps(timeline, _selection.SelectedNodes.ToList());
        }

        private void DeleteStep(MacroTimeline timeline, MacroNode node)
        {
            ResetTimelineDeleteConfirmation();

            SaveUndoSnapshot();
            var stepsToRemove = TimelineNodeMutationService.GetStepsToRemoveForDelete(timeline, node);
            if (timeline.UseStandardDelay && stepsToRemove.Any(TimelineNodeMutationService.IsDelayCleanupActionStep))
                TimelineNodeMutationService.AddStandardDelayCleanupSteps(timeline, stepsToRemove);

            foreach (var stepToRemove in stepsToRemove)
                timeline.Nodes.Remove(stepToRemove);

            MergeAdjacentDelayNodesIfEnabled(timeline);
            _selection.Clear();
            SelectTimeline(timeline);
            RefreshTimeline();
            ScheduleSaveState();
        }

        private void DeleteSteps(MacroTimeline timeline, IReadOnlyList<MacroNode> steps)
        {
            ResetTimelineDeleteConfirmation();

            var stepsToRemove = new List<MacroNode>();
            foreach (var step in steps)
            {
                var rawSteps = TimelineNodeMutationService.GetStepsToRemoveForDelete(timeline, step);
                if (timeline.UseStandardDelay && rawSteps.Any(TimelineNodeMutationService.IsDelayCleanupActionStep))
                    TimelineNodeMutationService.AddStandardDelayCleanupSteps(timeline, rawSteps);

                foreach (var rawStep in rawSteps)
                {
                    if (!stepsToRemove.Contains(rawStep))
                        stepsToRemove.Add(rawStep);
                }
            }

            if (stepsToRemove.Count == 0)
                return;

            SaveUndoSnapshot();
            foreach (var stepToRemove in stepsToRemove.OrderByDescending(timeline.Nodes.IndexOf))
                timeline.Nodes.Remove(stepToRemove);

            MergeAdjacentDelayNodesIfEnabled(timeline);
            _selection.Clear();
            SelectTimeline(timeline);
            RefreshTimeline();
            ScheduleSaveState();
        }

        private void EditTextStep(MacroTimeline timeline, MacroNode node)
        {
            var dialog = new TextInputWindow { Owner = this };
            dialog.SetText(node.Text);

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.ResultText))
                return;

            SaveUndoSnapshot();
            node.Text = dialog.ResultText;
            SelectTimeline(timeline);
            RefreshTimeline();
            ScheduleSaveState();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            CancelTimelineDragState();

            if (!_isClearConfirmationActive)
            {
                BeginClearConfirmation(_document.ActiveTimeline);
                return;
            }

            if (_pendingClearTimeline == null)
            {
                ResetClearConfirmation();
                return;
            }

            ClearTimeline(_pendingClearTimeline);
            ResetClearConfirmation();
        }


        private void BeginClearConfirmation(MacroTimeline timeline)
        {
            _pendingClearTimeline = timeline;
            _isClearConfirmationActive = true;

            var confirmText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            confirmText.Inlines.Add(new Run(timeline.Name)
            {
                FontWeight = FontWeights.Black,
                FontSize = 14
            });

            confirmText.Inlines.Add(new Run(" - Confirm")
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = 12
            });

            ClearButton.Content = confirmText;
            ClearButton.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            ClearButton.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            ClearButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        }

        private void ResetClearConfirmation()
        {
            _pendingClearTimeline = null;
            _isClearConfirmationActive = false;

            ClearButton.Content = "Clear";

            ClearButton.ClearValue(BackgroundProperty);
            ClearButton.ClearValue(BorderBrushProperty);
            ClearButton.ClearValue(ForegroundProperty);
        }

        private void ClearTimeline(MacroTimeline timeline)
        {
            ResetTimelineDeleteConfirmation();

            SaveUndoSnapshot();
            if (TryGetTimelineRunner(timeline, out var runner))
                runner.Stop();

            if (ReferenceEquals(_recordingTimeline, timeline))
                StopRecording();

            timeline.Nodes.Clear();

            if (ReferenceEquals(_selection.SelectedTimeline, timeline))
                _selection.Clear();

            SelectTimeline(timeline);
            RefreshTimeline();
            ScheduleSaveState();
        }

    // From MainWindow.DelayMerge.cs
        private bool MergeAdjacentDelayNodesIfEnabled(MacroTimeline timeline)
        {
            if (!_settings.MergeRepeatedDelayNodes)
                return false;

            var changed = TimelineNodeMutationService.MergeAdjacentDelayNodes(timeline);
            if (changed)
                PruneSelectionAfterDelayMerge(timeline);

            return changed;
        }


        private void PruneSelectionAfterDelayMerge(MacroTimeline timeline)
        {
            if (!ReferenceEquals(_selection.SelectedTimeline, timeline) || !_selection.HasNodeSelection)
                return;

            var selectedSteps = _selection.SelectedNodes
                .Where(timeline.Nodes.Contains)
                .ToList();

            if (selectedSteps.Count == 0)
            {
                _selection.Clear();
                return;
            }

            var currentAnchor = _selection.AnchorNode;
            var anchorStep = currentAnchor != null && timeline.Nodes.Contains(currentAnchor)
                ? currentAnchor
                : selectedSteps[^1];

            _selection.SelectNodes(timeline, selectedSteps, anchorStep);
        }

    // From MainWindow.NodeDragRawSteps.cs
        private List<MacroNode> GetRawStepsForDisplayStep(IReadOnlyCollection<MacroNode> rawSteps, MacroNode node)
        {
            if (node.IsSyntheticDisplayNode)
            {
                return node.SourceNodes
                    .Where(rawSteps.Contains)
                    .ToList();
            }

            return rawSteps.Contains(node)
                ? new List<MacroNode> { node }
                : new List<MacroNode>();
        }

}
