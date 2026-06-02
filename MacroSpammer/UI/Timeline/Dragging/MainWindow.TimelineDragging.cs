using System.Windows;
using System.Windows.Input;
using MacroSpammer.Domain;
using System.Windows.Media;
using MacroSpammer.UI.Config;
using MacroSpammer.UI.Nodes;
using MacroSpammer.UI.Timeline;

namespace MacroSpammer;

public partial class MainWindow
{
    private readonly NodeDragPreviewModel _nodeDragPreview = new();

    private FrameworkElement? _draggedStepGhost;
    private TranslateTransform? _draggedStepGhostTransform;

    private double _draggedStepGhostWidth;
    private double _draggedStepGhostHeight;

    private Point? _lastDroppedGhostRowsPanelPosition;

    private Point _dragGhostCurrentPosition;
    private Point _dragGhostTargetPosition;

    private bool _isDragGhostAnimating;
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

            SetPendingClickSelection(
                timeline,
                node,
                Keyboard.Modifiers,
                _selection.IsNodeSelected(timeline, node));

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
            SelectTimeline(_pendingClickSelectionTimeline);
            _selection.SelectNode(_pendingClickSelectionTimeline, _pendingClickSelectionStep);
        }

        ClearPendingClickSelection();
    }

    private void SelectStepFromStoredClick(MacroTimeline timeline, MacroNode node, ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            SelectTimeline(timeline);
            ToggleStepSelection(timeline, node);
            RefreshInspector();
            return;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift) && _selection.AnchorNode != null &&
            ReferenceEquals(_selection.SelectedTimeline, timeline))
        {
            SelectTimeline(timeline);
            SelectStepRange(timeline, node);
            RefreshInspector();
            return;
        }

        SelectTimeline(timeline);
        _selection.SelectNode(timeline, node);
        RefreshInspector();
    }

    private void ClearPendingClickSelection()
    {
        _pendingClickSelectionTimeline = null;
        _pendingClickSelectionStep = null;
        _pendingClickSelectionModifiers = ModifierKeys.None;
        _pendingClickWasSelected = false;
    }
}
