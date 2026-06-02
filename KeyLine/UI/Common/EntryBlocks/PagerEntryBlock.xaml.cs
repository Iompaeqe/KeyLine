using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KeyLine.UI.Common.EntryBlocks;

public partial class PagerEntryBlock : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(PagerEntryBlock),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueMinWidthProperty = DependencyProperty.Register(
        nameof(ValueMinWidth),
        typeof(double),
        typeof(PagerEntryBlock),
        new PropertyMetadata(30d));

    public event EventHandler? PageRequested;

    public PagerEntryBlock()
    {
        InitializeComponent();

        PreviousTextBlock.MouseLeftButtonDown += (_, e) => RaisePageRequested(e);
        ValueTextBlock.MouseLeftButtonDown += (_, e) => RaisePageRequested(e);
        NextTextBlock.MouseLeftButtonDown += (_, e) => RaisePageRequested(e);
        PreviousTextBlock.MouseEnter += (_, _) => SetGlyphOpacity(PreviousTextBlock, 1);
        PreviousTextBlock.MouseLeave += (_, _) => SetGlyphOpacity(PreviousTextBlock, IsEnabled ? 0.8 : 0.35);
        NextTextBlock.MouseEnter += (_, _) => SetGlyphOpacity(NextTextBlock, 1);
        NextTextBlock.MouseLeave += (_, _) => SetGlyphOpacity(NextTextBlock, IsEnabled ? 0.8 : 0.35);
        IsEnabledChanged += (_, _) => ValueTextBlock.Cursor = IsEnabled ? Cursors.Hand : Cursors.Arrow;
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double ValueMinWidth
    {
        get => (double)GetValue(ValueMinWidthProperty);
        set => SetValue(ValueMinWidthProperty, value);
    }

    private void RaisePageRequested(MouseButtonEventArgs e)
    {
        if (!IsEnabled)
            return;

        PageRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void SetGlyphOpacity(TextBlock glyph, double opacity)
    {
        if (IsEnabled)
            glyph.Opacity = opacity;
    }
}
