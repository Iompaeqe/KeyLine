using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroSpammer.UI.Timeline;

public sealed class NodeDragGhostController
{
    private readonly Canvas _overlayCanvas;

    private FrameworkElement? _ghost;
    private TranslateTransform? _transform;

    private double _width;
    private double _height;

    private Point _currentPosition;
    private Point _targetPosition;

    private bool _isAnimating;

    public NodeDragGhostController(Canvas overlayCanvas)
    {
        _overlayCanvas = overlayCanvas;
    }

    public bool HasGhost => _ghost != null;
    public double Width => _width;
    public double Height => _height;
    public Point? LastDroppedRowsPanelPosition { get; private set; }

    public void Begin(
        UIElement ghostElement,
        Size size,
        double opacity,
        double scale,
        Point initialTargetPosition)
    {
        End();

        if (ghostElement is not FrameworkElement frameworkElement)
            return;

        frameworkElement.IsHitTestVisible = false;
        frameworkElement.Opacity = opacity;
        frameworkElement.RenderTransformOrigin = new Point(0.5, 0.5);

        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(new ScaleTransform(scale, scale));

        _transform = new TranslateTransform();
        transformGroup.Children.Add(_transform);

        frameworkElement.RenderTransform = transformGroup;

        _width = size.Width;
        _height = size.Height;
        _ghost = frameworkElement;

        _overlayCanvas.Children.Add(frameworkElement);

        Canvas.SetLeft(frameworkElement, 0);
        Canvas.SetTop(frameworkElement, 0);
        Panel.SetZIndex(frameworkElement, 1000);

        UpdateTarget(initialTargetPosition, snap: true);
    }

    public void UpdateTarget(Point targetPosition, bool snap = false)
    {
        if (!HasGhost)
            return;

        _targetPosition = targetPosition;

        if (!snap)
            return;

        _currentPosition = _targetPosition;
        ApplyPosition();
    }

    public void MoveTowardTarget(double followStrength)
    {
        if (_transform == null || _ghost == null)
            return;

        var dx = _targetPosition.X - _currentPosition.X;
        var dy = _targetPosition.Y - _currentPosition.Y;

        if (Math.Abs(dx) < 0.2 && Math.Abs(dy) < 0.2)
        {
            _currentPosition = _targetPosition;
        }
        else
        {
            _currentPosition = new Point(
                _currentPosition.X + (dx * followStrength),
                _currentPosition.Y + (dy * followStrength));
        }

        ApplyPosition();
    }

    public void StartAnimation(EventHandler renderingHandler)
    {
        if (_isAnimating)
            return;

        _isAnimating = true;
        CompositionTarget.Rendering += renderingHandler;
    }

    public void StopAnimation(EventHandler renderingHandler)
    {
        if (!_isAnimating)
            return;

        _isAnimating = false;
        CompositionTarget.Rendering -= renderingHandler;
    }

    public void CaptureDropPosition(FrameworkElement rowsPanel, double itemTop)
    {
        if (_transform == null)
        {
            LastDroppedRowsPanelPosition = null;
            return;
        }

        var ghostPositionInRowsPanel = _overlayCanvas.TranslatePoint(
            new Point(_transform.X, _transform.Y),
            rowsPanel);

        LastDroppedRowsPanelPosition = new Point(ghostPositionInRowsPanel.X, itemTop);
    }

    public void ClearDropPosition()
    {
        LastDroppedRowsPanelPosition = null;
    }

    public void End()
    {
        if (_ghost != null)
            _overlayCanvas.Children.Remove(_ghost);

        _ghost = null;
        _transform = null;

        _width = 0;
        _height = 0;

        _currentPosition = default;
        _targetPosition = default;
        LastDroppedRowsPanelPosition = null;
    }

    private void ApplyPosition()
    {
        if (_transform == null)
            return;

        _transform.X = _currentPosition.X;
        _transform.Y = _currentPosition.Y;
    }
}
