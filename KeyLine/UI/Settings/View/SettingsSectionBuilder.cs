using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using KeyLine.Services.Features;
using KeyLine.Services.Input;
using KeyLine.UI;

namespace KeyLine;

public sealed class SettingsSectionBuilder
{
    private readonly StackPanel _settingsPanel;
    private readonly ISettingsActions _actions;
    private readonly Action<Button, Action<string>> _beginShortcutCapture;

    public SettingsSectionBuilder(
        StackPanel settingsPanel,
        ISettingsActions actions,
        Action<Button, Action<string>> beginShortcutCapture)
    {
        _settingsPanel = settingsPanel;
        _actions = actions;
        _beginShortcutCapture = beginShortcutCapture;
    }

    public void BeginSection(string title)
    {
        _settingsPanel.Children.Clear();
        _settingsPanel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        });
    }
    
    public void Add(UIElement element)
    {
        _settingsPanel.Children.Add(element);
    }

    public void AddCheck(string text, bool value, Action<bool> changed, bool isEnabled = true)
    {
        var checkBox = new CheckBox
        {
            Content = text,
            IsChecked = value,
            Margin = new Thickness(0, 0, 0, 9),
            Foreground = isEnabled ? Brushes.White : DimBrush
        };

        ApplySettingsCheckBoxStyle(checkBox, isEnabled);
        checkBox.Checked += (_, _) => changed(true);
        checkBox.Unchecked += (_, _) => changed(false);
        _settingsPanel.Children.Add(checkBox);
    }

    public void AddNumber(string label, int value, Action<int> changed, string unit)
    {
        var panel = AddRow(label);
        var box = new TextBox
        {
            Text = value.ToString(),
            Width = 86,
            Height = 24,
            Margin = new Thickness(0, 0, 6, 0)
        };

        box.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        box.LostFocus += (_, _) =>
        {
            if (int.TryParse(box.Text, out var parsed))
                changed(Math.Max(0, parsed));
        };

        panel.Children.Add(box);

        if (!string.IsNullOrWhiteSpace(unit))
        {
            panel.Children.Add(new TextBlock
            {
                Text = unit,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DimBrush
            });
        }
    }

    public void AddChoice(string label, string value, IEnumerable<string> options, Action<string> changed)
    {
        var panel = AddRow(label);
        var comboBox = new ComboBox
        {
            Width = 112,
            Height = 24,
            ItemsSource = options.ToArray(),
            SelectedItem = value
        };

        comboBox.SelectionChanged += (_, _) =>
        {
            if (comboBox.SelectedItem is string selected)
                changed(selected);
        };

        panel.Children.Add(comboBox);
    }

    public void AddShortcut(string label, string shortcut, Action<string> changed)
    {
        var panel = AddRow(label);
        var button = new Button
        {
            Content = ShortcutGesture.Format(shortcut),
            Width = 150,
            Height = 24
        };

        button.Click += (_, _) => _beginShortcutCapture(button, changed);
        panel.Children.Add(button);
    }

    public void AddAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 0, 8)
        };

        button.Click += (_, _) => action();
        _settingsPanel.Children.Add(button);
    }

    public void AddFeatureAction(string label, FeatureId feature, Action action)
    {
        if (!_actions.IsFeatureVisible(feature))
            return;

        var isLocked = _actions.IsFeatureLocked(feature);
        var button = new Button
        {
            Content = isLocked ? $"{label} (locked)" : label,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = isLocked ? 0.55 : 1.0,
            ToolTip = isLocked ? _actions.GetLockedFeatureMessage(feature) : null
        };

        button.Click += (_, _) => action();
        _settingsPanel.Children.Add(button);
    }

    public void AddDescription(string text)
    {
        _settingsPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    public void AddNotice(string text)
    {
        _settingsPanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            Margin = new Thickness(0, 2, 0, 8)
        });
    }

    public void AddLicenseLink()
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = DimBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };

        textBlock.Inlines.Add(new Run("Licensed under the "));

        var link = new Hyperlink(new Run("GNU General Public License v3.0"))
        {
            Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
            ToolTip = SettingsExternalLinks.LicenseUrl
        };

        link.Click += (_, _) => SettingsExternalLinks.Open(SettingsExternalLinks.LicenseUrl);

        textBlock.Inlines.Add(link);
        textBlock.Inlines.Add(new Run("."));
        _settingsPanel.Children.Add(textBlock);
    }

    public void AddFeedbackButton()
    {
        var panel = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var button = new Button
        {
            Content = "Feedback / Bug report",
            Height = 28,
            MinWidth = 170,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = "Open the KeyLine GitHub issue page."
        };

        button.Click += (_, _) => SettingsExternalLinks.Open(SettingsExternalLinks.FeedbackUrl);

        DockPanel.SetDock(button, Dock.Right);
        panel.Children.Add(button);
        _settingsPanel.Children.Add(panel);
    }

    public void AddSpacer(double height)
    {
        _settingsPanel.Children.Add(new Border
        {
            Height = height,
            Background = Brushes.Transparent
        });
    }

    private StackPanel AddRow(string label)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 9)
        };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        });

        _settingsPanel.Children.Add(panel);
        return panel;
    }

    private static void ApplySettingsCheckBoxStyle(CheckBox checkBox, bool isEnabled)
    {
        checkBox.IsHitTestVisible = isEnabled;
        checkBox.Focusable = isEnabled;
        checkBox.Opacity = isEnabled ? 1.0 : 0.42;

        if (isEnabled)
            return;

        checkBox.ToolTip = TooltipNotes.StandardDelayRequiresEnable;
        checkBox.Template = CreateDisabledCheckBoxTemplate();
    }

    private static ControlTemplate CreateDisabledCheckBoxTemplate()
    {
        var template = new ControlTemplate(typeof(CheckBox));

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var box = new FrameworkElementFactory(typeof(Border));
        box.SetValue(FrameworkElement.WidthProperty, 14.0);
        box.SetValue(FrameworkElement.HeightProperty, 14.0);
        box.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 7, 0));
        box.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        box.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        box.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85)));
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var mark = new FrameworkElementFactory(typeof(TextBlock));
        mark.SetValue(TextBlock.TextProperty, "✓");
        mark.SetValue(TextBlock.FontSizeProperty, 10.0);
        mark.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        mark.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
        mark.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        mark.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        box.AppendChild(mark);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(TextElement.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 116, 139)));

        panel.AppendChild(box);
        panel.AppendChild(content);
        template.VisualTree = panel;
        return template;
    }

    public static Brush DimBrush => new SolidColorBrush(Color.FromRgb(142, 160, 182));
}
