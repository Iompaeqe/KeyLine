using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KeyLine.Domain;

namespace KeyLine;

public sealed record ImportProfilePreview(string Name, IReadOnlyList<string> MacroNames);

public sealed record ImportSettingsPreview(
    int MacroCount,
    int ProfileCount,
    int ActiveWorkspaceIndex,
    string ActiveProfileName,
    bool ShortcutsEnabled,
    int DefaultTimerMs,
    int DefaultBaseDelayMs,
    int DefaultLoopCount,
    MacroLoopMode DefaultLoopMode,
    bool ExperimentalFeaturesEnabled);

public partial class ImportConfirmationView : UserControl
{
    private readonly Action _confirmRequested;
    private readonly Action _cancelRequested;

    public ImportConfirmationView(
        string title,
        string confirmText,
        IReadOnlyList<ImportProfilePreview> profiles,
        Action confirmRequested,
        Action cancelRequested,
        string? warningText = null,
        ImportSettingsPreview? settingsPreview = null)
    {
        _confirmRequested = confirmRequested;
        _cancelRequested = cancelRequested;

        InitializeComponent();

        TitleTextBlock.Text = title;
        ConfirmButton.Content = confirmText;

        if (!string.IsNullOrWhiteSpace(warningText))
        {
            WarningTextBlock.Text = warningText;
            WarningBorder.Visibility = Visibility.Visible;
            ConfirmButton.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            ConfirmButton.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            ConfirmButton.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
        }

        if (settingsPreview != null)
            AddSettingsSummary(settingsPreview);

        AddProfiles(profiles);
    }

    private void AddSettingsSummary(ImportSettingsPreview preview)
    {
        AddHeader("Settings summary");
        AddLine($"Profiles: {preview.ProfileCount}");
        AddLine($"Macros: {preview.MacroCount}");
        AddLine($"Active profile: {preview.ActiveProfileName}");
        AddLine($"Active macro index: {preview.ActiveWorkspaceIndex + 1}");
        AddLine($"Macro shortcuts: {(preview.ShortcutsEnabled ? "enabled" : "disabled")}");
        AddLine($"Default timer: {preview.DefaultTimerMs} ms");
        AddLine($"Default base delay: {preview.DefaultBaseDelayMs} ms");
        AddLine($"Default loop count: {preview.DefaultLoopCount}");
        AddLine($"Default loop mode: {preview.DefaultLoopMode}");
        AddLine($"Experimental features: {(preview.ExperimentalFeaturesEnabled ? "enabled" : "disabled")}");
        AddSpacer();
    }

    private void AddProfiles(IReadOnlyList<ImportProfilePreview> profiles)
    {
        AddHeader("Import contents");

        if (profiles.Count == 0)
        {
            AddLine("No profiles or macros found.");
            return;
        }

        foreach (var profile in profiles)
        {
            AddProfileName(profile.Name);

            if (profile.MacroNames.Count == 0)
            {
                AddLine("No macros", leftMargin: 12);
                continue;
            }

            foreach (var macroName in profile.MacroNames)
                AddLine(macroName, leftMargin: 12);
        }
    }

    private void AddHeader(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(229, 238, 248)),
            Margin = new Thickness(0, 0, 0, 6)
        });
    }

    private void AddProfileName(string text)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
            Margin = new Thickness(0, 4, 0, 3)
        });
    }

    private void AddLine(string text, double leftMargin = 0)
    {
        ContentPanel.Children.Add(new TextBlock
        {
            Text = leftMargin > 0 ? $"- {text}" : text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(184, 199, 218)),
            Margin = new Thickness(leftMargin, 0, 0, 4)
        });
    }

    private void AddSpacer()
    {
        ContentPanel.Children.Add(new Border { Height = 8, Opacity = 0 });
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _confirmRequested();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested();
    }
}
