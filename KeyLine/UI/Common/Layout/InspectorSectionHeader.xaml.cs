using System.Windows;
using System.Windows.Controls;

namespace KeyLine.UI.Common.Layout;

public partial class InspectorSectionHeader : UserControl
{
    public InspectorSectionHeader()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(InspectorSectionHeader),
            new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty ArrowTextProperty =
        DependencyProperty.Register(
            nameof(ArrowText),
            typeof(string),
            typeof(InspectorSectionHeader),
            new PropertyMetadata("▼"));

    public string ArrowText
    {
        get => (string)GetValue(ArrowTextProperty);
        set => SetValue(ArrowTextProperty, value);
    }

    public static readonly DependencyProperty HeaderToolTipProperty =
        DependencyProperty.Register(
            nameof(HeaderToolTip),
            typeof(object),
            typeof(InspectorSectionHeader),
            new PropertyMetadata(null));

    public object? HeaderToolTip
    {
        get => GetValue(HeaderToolTipProperty);
        set => SetValue(HeaderToolTipProperty, value);
    }

    public static readonly DependencyProperty HeaderMarginProperty =
        DependencyProperty.Register(
            nameof(HeaderMargin),
            typeof(Thickness),
            typeof(InspectorSectionHeader),
            new PropertyMetadata(new Thickness(0, 0, 0, 4)));

    public Thickness HeaderMargin
    {
        get => (Thickness)GetValue(HeaderMarginProperty);
        set => SetValue(HeaderMarginProperty, value);
    }
}