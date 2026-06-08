using System.Diagnostics;

namespace KeyLine;

public static class SettingsExternalLinks
{
    public const string LicenseUrl = "https://github.com/Iompaeqe/KeyLine-code/tree/1.8?tab=GPL-3.0-1-ov-file";
    public const string FeedbackUrl = "https://github.com/Iompaeqe/KeyLine/issues/new";

    public static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Optional external link. Ignore if Windows cannot open it.
        }
    }
}
