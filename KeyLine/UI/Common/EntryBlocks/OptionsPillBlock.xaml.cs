using System.Windows;
using System.Windows.Controls;

namespace KeyLine.UI.Common.EntryBlocks;

public partial class OptionsPillBlock : UserControl
{
    public OptionsPillBlock()
    {
        InitializeComponent();

        SetResourceReference(BorderStyleProperty, "OptionsShortcutBorder");
        SetResourceReference(TextStyleProperty, "OptionsShortcutText");
        SetResourceReference(TextBoxStyleProperty, "OptionsPillTextBox");
        SetResourceReference(PlaceholderStyleProperty, "OptionsPillPlaceholderText");

        InputTextBox.GotKeyboardFocus += (_, _) => UpdatePlaceholder();
        InputTextBox.LostKeyboardFocus += (_, _) => UpdatePlaceholder();
        InputTextBox.TextChanged += (_, _) => UpdatePlaceholder();
        UpdateMode();
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(OptionsPillBlock),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(OptionsPillBlock),
            new PropertyMetadata(string.Empty, OnPlaceholderChanged));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(
            nameof(DisplayText),
            typeof(string),
            typeof(OptionsPillBlock),
            new PropertyMetadata(string.Empty));

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        private set => SetValue(DisplayTextProperty, value);
    }

    public static readonly DependencyProperty IsTextInputProperty =
        DependencyProperty.Register(
            nameof(IsTextInput),
            typeof(bool),
            typeof(OptionsPillBlock),
            new PropertyMetadata(false, OnModeChanged));

    public bool IsTextInput
    {
        get => (bool)GetValue(IsTextInputProperty);
        set => SetValue(IsTextInputProperty, value);
    }

    public static readonly DependencyProperty InputWidthProperty =
        DependencyProperty.Register(
            nameof(InputWidth),
            typeof(double),
            typeof(OptionsPillBlock),
            new PropertyMetadata(126.0));

    public double InputWidth
    {
        get => (double)GetValue(InputWidthProperty);
        set => SetValue(InputWidthProperty, value);
    }

    public static readonly DependencyProperty BorderStyleProperty =
        DependencyProperty.Register(
            nameof(BorderStyle),
            typeof(Style),
            typeof(OptionsPillBlock),
            new PropertyMetadata(null));

    public Style? BorderStyle
    {
        get => (Style?)GetValue(BorderStyleProperty);
        set => SetValue(BorderStyleProperty, value);
    }

    public static readonly DependencyProperty TextStyleProperty =
        DependencyProperty.Register(
            nameof(TextStyle),
            typeof(Style),
            typeof(OptionsPillBlock),
            new PropertyMetadata(null));

    public Style? TextStyle
    {
        get => (Style?)GetValue(TextStyleProperty);
        set => SetValue(TextStyleProperty, value);
    }

    public static readonly DependencyProperty TextBoxStyleProperty =
        DependencyProperty.Register(
            nameof(TextBoxStyle),
            typeof(Style),
            typeof(OptionsPillBlock),
            new PropertyMetadata(null));

    public Style? TextBoxStyle
    {
        get => (Style?)GetValue(TextBoxStyleProperty);
        set => SetValue(TextBoxStyleProperty, value);
    }

    public static readonly DependencyProperty PlaceholderStyleProperty =
        DependencyProperty.Register(
            nameof(PlaceholderStyle),
            typeof(Style),
            typeof(OptionsPillBlock),
            new PropertyMetadata(null));

    public Style? PlaceholderStyle
    {
        get => (Style?)GetValue(PlaceholderStyleProperty);
        set => SetValue(PlaceholderStyleProperty, value);
    }

    public Border BorderElement => PillBorder;
    public TextBlock TextElement => DisplayTextBlock;
    public TextBox InputElement => InputTextBox;

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OptionsPillBlock)d).UpdateMode();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var block = (OptionsPillBlock)d;
        block.UpdateDisplayText();
        block.UpdatePlaceholder();
    }

    private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var block = (OptionsPillBlock)d;
        block.UpdateDisplayText();
        block.UpdatePlaceholder();
    }

    private void UpdateMode()
    {
        if (DisplayTextBlock == null || InputTextBox == null || PlaceholderTextBlock == null)
            return;

        DisplayTextBlock.Visibility = IsTextInput ? Visibility.Collapsed : Visibility.Visible;
        InputTextBox.Visibility = IsTextInput ? Visibility.Visible : Visibility.Collapsed;
        UpdateDisplayText();
        UpdatePlaceholder();
    }

    private void UpdateDisplayText()
    {
        DisplayText = string.IsNullOrWhiteSpace(Text) && !string.IsNullOrWhiteSpace(PlaceholderText)
            ? PlaceholderText
            : Text;
    }

    private void UpdatePlaceholder()
    {
        if (PlaceholderTextBlock == null || InputTextBox == null)
            return;

        PlaceholderTextBlock.Visibility = IsTextInput &&
                                          !InputTextBox.IsKeyboardFocusWithin &&
                                          string.IsNullOrEmpty(Text) &&
                                          !string.IsNullOrEmpty(PlaceholderText)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void FocusInput()
    {
        if (IsTextInput)
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }
        else
        {
            Focus();
        }
    }
}
