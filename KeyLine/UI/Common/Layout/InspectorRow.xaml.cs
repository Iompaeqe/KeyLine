using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace KeyLine.UI.Common.Layout;

[ContentProperty(nameof(RowContent))]
public partial class InspectorRow : UserControl
{
    public InspectorRow()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(InspectorRow),
            new PropertyMetadata(string.Empty));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty RowToolTipProperty =
        DependencyProperty.Register(
            nameof(RowToolTip),
            typeof(object),
            typeof(InspectorRow),
            new PropertyMetadata(null));

    public object? RowToolTip
    {
        get => GetValue(RowToolTipProperty);
        set => SetValue(RowToolTipProperty, value);
    }

    public static readonly DependencyProperty RowContentProperty =
        DependencyProperty.Register(
            nameof(RowContent),
            typeof(object),
            typeof(InspectorRow),
            new PropertyMetadata(null));

    public object? RowContent
    {
        get => GetValue(RowContentProperty);
        set => SetValue(RowContentProperty, value);
    }

    public static readonly DependencyProperty RowMarginProperty =
        DependencyProperty.Register(
            nameof(RowMargin),
            typeof(Thickness),
            typeof(InspectorRow),
            new PropertyMetadata(new Thickness(0, 0, 0, 2)));

    public Thickness RowMargin
    {
        get => (Thickness)GetValue(RowMarginProperty);
        set => SetValue(RowMarginProperty, value);
    }

    public T? GetContent<T>() where T : class
    {
        return RowContent as T;
    }
}